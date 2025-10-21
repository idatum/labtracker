using Microsoft.Extensions.Options;

namespace LabTracker;

/// <summary>
/// Background service that monitors UniFi Access Points client connections.
/// Uses IClientInfoProvider interface for retrieving client status.
/// </summary>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly Options _options;
    private readonly IPublisher _publisher;
    private readonly IPublished _publishedReader;
    private readonly IClientInfoProvider _clientProvider;
    
    /// <summary>
    /// Tracks the last known client list for each Access Point to detect changes.
    /// Key: AP hostname, Value: List of client identifiers (MAC or hostname based on configuration)
    /// </summary>
    private readonly Dictionary<string, List<string>> _lastClientsByAp = new();

    /// <summary>
    /// Initializes a new instance of the Worker class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output</param>
    /// <param name="hostApplicationLifetime">Application lifetime manager</param>
    /// <param name="sshOptions">Configuration options for SSH connections and MQTT publishing</param>
    /// <param name="publisher">Publisher interface for sending client connection events</param>
    /// <param name="publishedReader">Published state reader for initialization</param>
    /// <param name="clientProvider">Client information provider for retrieving client data</param>
    public Worker(ILogger<Worker> logger, IHostApplicationLifetime hostApplicationLifetime, IOptions<Options> sshOptions, IPublisher publisher, IPublished publishedReader, IClientInfoProvider clientProvider)
    {
        _logger = logger;
        _hostApplicationLifetime = hostApplicationLifetime;
        _options = sshOptions.Value;
        _publisher = publisher;
        _publishedReader = publishedReader;
        _clientProvider = clientProvider;
    }
    
    /// <summary>
    /// Main execution loop for the background service.
    /// Initializes the publisher, then continuously polls UniFi Access Points
    /// for client changes at the configured interval.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to stop the service</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initialize the publisher (MQTT connection, etc.)
        await _publisher.InitializeAsync();
        
        // Read current published client states to initialize our tracking
        await InitializeClientStatesAsync();
        
        // Main monitoring loop - runs until service is stopped
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogDebug("Worker running at: {time}", DateTimeOffset.Now);
                await Process(stoppingToken);
                await Task.Delay(_options.DelayMs, stoppingToken);
            }
            catch (SshConnectionException)
            {
                _logger.LogWarning("SSH connection failure detected. Processing will restart.");
                // Break out of the loop to allow service restart
                await Task.Delay(_options.DelayMs, stoppingToken);
                break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected cancellation, exit gracefully
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in worker execution loop");
                // For other exceptions, wait and continue
                await Task.Delay(_options.DelayMs, stoppingToken);
            }
        }
        
        // Gracefully stop the application when cancellation is requested
        _hostApplicationLifetime.StopApplication();
    }

    /// <summary>
    /// Initialize client states from current published MQTT messages
    /// </summary>
    private async Task InitializeClientStatesAsync()
    {
        try
        {
            var currentStates = await _publishedReader.ReadCurrentStatesAsync();
            _logger.LogInformation("Found {count} existing client states", currentStates.Count);
            // Group states by AP and initialize _lastClientsByAp
            foreach (var state in currentStates.Values.Where(s => s.IsConnected))
            {
                var apHostname = _options.Mqtt.IncludeApInTopic
                    ? (state.ApHostname ?? "unknown")
                    : Options.AllApsAggregate;

                if (!_lastClientsByAp.ContainsKey(apHostname))
                {
                    _lastClientsByAp[apHostname] = [];
                }

                _lastClientsByAp[apHostname].Add(state.ClientId);

                _logger.LogDebug("Initialized client {clientId} as connected to {ap}",
                    state.ClientId, apHostname);
            }
            foreach (var ap in _lastClientsByAp)
            {
                _logger.LogInformation("AP {ap} initialized with {count} connected clients: {clients}",
                    ap.Key, ap.Value.Count, string.Join(", ", ap.Value));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize client states from published messages");
        }
    }

    /// <summary>
    /// Main processing loop that connects to all configured UniFi Access Points.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to stop processing</param>
    private async Task Process(CancellationToken stoppingToken)
    {
        var results = new List<(string? hostname, List<ClientInfo> clients)>();
        var failureCount = 0;

        // Process each host individually to handle failures gracefully
        foreach (var sshHost in _options.Unifi.AccessPoints)
        {
            try
            {
                var result = await ProcessHost(sshHost, stoppingToken);
                results.Add(result);
            }
            catch (SshConnectionException ex)
            {
                _logger.LogWarning(ex, "SSH connection failed for host {host}", sshHost);
                failureCount++;
                // Add empty result to maintain consistency
                results.Add((null, new List<ClientInfo>()));
            }
        }

        // If any SSH connections failed, trigger a service restart
        if (failureCount > 0)
        {
            _logger.LogError("SSH connection failures detected ({failureCount}/{totalHosts}). Triggering service restart.", 
                failureCount, _options.Unifi.AccessPoints.Length);
            _hostApplicationLifetime.StopApplication();
            throw new SshConnectionException($"SSH failures detected: {failureCount}/{_options.Unifi.AccessPoints.Length} hosts failed");
        }

        await ProcessClients(results.ToArray(), stoppingToken);
    }

    /// <summary>
    /// Processes a single UniFi AP host using the client provider.
    /// </summary>
    /// <param name="sshHost">IP address or hostname of the UniFi AP</param>
    /// <param name="stoppingToken">Cancellation token to abort processing</param>
    /// <returns>Tuple containing the AP hostname and list of connected clients</returns>
    private async Task<(string? hostname, List<ClientInfo> clients)> ProcessHost(string sshHost, CancellationToken stoppingToken)
    {
        if (stoppingToken.IsCancellationRequested)
        {
            return (null, new List<ClientInfo>());
        }
        
        return await _clientProvider.GetClientsAsync(sshHost, stoppingToken);
    }
    
    /// <summary>
    /// Processes the client connection changes and publishes events.
    /// </summary>
    /// <param name="results">Results from processing all APs</param>
    /// <param name="stoppingToken">Cancellation token</param>
    private async Task ProcessClients((string? hostname, List<ClientInfo> clients)[] results, CancellationToken stoppingToken)
    {
        if (stoppingToken.IsCancellationRequested) return;

        var allConnectedClients = new HashSet<string>();
        var clientDiffPerAp = new Dictionary<string, (List<string> newClients, List<string> disconnectedClients)>();

        if (_options.Mqtt.IncludeApInTopic)
        {
            ProcessClientsPerAp(results, allConnectedClients, clientDiffPerAp, stoppingToken);
        }
        else
        {
            ProcessClientsAggregated(results, allConnectedClients, clientDiffPerAp, stoppingToken);
        }

        await PublishClientChanges(clientDiffPerAp, allConnectedClients);
    }

    /// <summary>
    /// Extract valid client IDs from client list
    /// </summary>
    private List<string> ExtractClientIds(List<ClientInfo> clients, string? apHostname, HashSet<string> allConnectedClients)
    {
        var clientIds = new List<string>();
        
        foreach (var client in clients)
        {
            _logger.LogDebug("Client {mac} : {name}", client.Mac, client.DisplayName);
            var clientId = client.GetClientId(_options.Mqtt.UseHostname);
            
            if (!string.IsNullOrEmpty(clientId) && clientId != "Unknown")
            {
                clientIds.Add(clientId);
                allConnectedClients.Add(clientId);
            }
            else
            {
                _logger.LogWarning("Client entry missing valid identifier: {client}", client);
            }
        }
        
        _logger.LogDebug("AP {hostname} has {count} clients: {clients}", 
            apHostname, clientIds.Count, string.Join(", ", clientIds));
        
        return clientIds;
    }

    /// <summary>
    /// Calculate the difference between current and last known client states
    /// </summary>
    private (List<string> newClients, List<string> disconnectedClients) CalculateClientDiff(string apKey, List<string> currentClients)
    {
        if (_lastClientsByAp.TryGetValue(apKey, out var lastClients))
        {
            var newClients = currentClients.Except(lastClients).ToList();
            var disconnectedClients = lastClients.Except(currentClients).ToList();
            return (newClients, disconnectedClients);
        }
        else
        {
            // First time seeing this AP, all clients are "new"
            return (currentClients.ToList(), new List<string>());
        }
    }

    /// <summary>
    /// Log client connection changes
    /// </summary>
    private void LogClientChanges(
        string apName, List<string> newClients, List<string> disconnectedClients,
        int totalClientCount, List<ClientInfo>? clientInfos)
    {
        foreach (var newClient in newClients)
        {
            _logger.LogInformation("New client connected to {hostname}: {client}", apName, newClient);
        }
        
        foreach (var disconnectedClient in disconnectedClients)
        {
            _logger.LogInformation("Client disconnected from {hostname}: {client}", apName, disconnectedClient);
        }

        // Log initial state if this is the first time we're seeing this AP
        if (!_lastClientsByAp.ContainsKey(apName))
        {
            if (clientInfos != null)
            {
                _logger.LogInformation("AP {hostname} initially has {count} clients: {clients}", 
                    apName, totalClientCount, string.Join(", ", clientInfos.Select(c => c.DisplayName)));
            }
            else
            {
                _logger.LogInformation("{hostname} initially has {count} clients: {clients}", 
                    apName, totalClientCount, string.Join(", ", newClients));
            }
        }
    }

    /// <summary>
    /// Process clients separately for each AP when AP is included in topic
    /// </summary>
    private void ProcessClientsPerAp(
        (string? hostname, List<ClientInfo> clients)[] results,
        HashSet<string> allConnectedClients,
        Dictionary<string, (List<string> newClients, List<string> disconnectedClients)> clientDiffPerAp,
        CancellationToken stoppingToken)
    {
        foreach (var (apHostname, clients) in results.Where(r => !string.IsNullOrEmpty(r.hostname)))
        {
            if (stoppingToken.IsCancellationRequested) return;

            var clientIds = ExtractClientIds(clients, apHostname, allConnectedClients);
            var (newClients, disconnectedClients) = CalculateClientDiff(apHostname!, clientIds);
            
            LogClientChanges(apHostname!, newClients, disconnectedClients, clientIds.Count, clients);
            
            clientDiffPerAp[apHostname!] = (newClients, disconnectedClients);
            _lastClientsByAp[apHostname!] = clientIds;
        }
    }

    /// <summary>
    /// Process all clients aggregated under a single aggregate name when AP is not included in topic
    /// </summary>
    private void ProcessClientsAggregated(
        (string? hostname, List<ClientInfo> clients)[] results,
        HashSet<string> allConnectedClients,
        Dictionary<string, (List<string> newClients, List<string> disconnectedClients)> clientDiffPerAp,
        CancellationToken stoppingToken)
    {
        var allClientIds = new List<string>();
        
        foreach (var (apHostname, clients) in results.Where(r => !string.IsNullOrEmpty(r.hostname)))
        {
            if (stoppingToken.IsCancellationRequested) return;
            
            _logger.LogDebug("Processing AP {hostname} with {count} clients", apHostname, clients.Count);
            var clientIds = ExtractClientIds(clients, apHostname, allConnectedClients);
            allClientIds.AddRange(clientIds);
        }

        var (newClients, disconnectedClients) = CalculateClientDiff(Options.AllApsAggregate, allClientIds);
        
        LogClientChanges(Options.AllApsAggregate, newClients, disconnectedClients, allClientIds.Count, null);
        
        clientDiffPerAp[Options.AllApsAggregate] = (newClients, disconnectedClients);
        _lastClientsByAp[Options.AllApsAggregate] = allClientIds;
    }

    /// <summary>
    /// Publish client changes via MQTT
    /// </summary>
    private async Task PublishClientChanges(
        Dictionary<string, (List<string> newClients, List<string> disconnectedClients)> clientDiffPerAp,
        HashSet<string> allConnectedClients)
    {
        if (_options.Mqtt.IncludeApInTopic)
        {
            // Publish events for each AP separately
            foreach (var (ap, (newClients, disconnectedClients)) in clientDiffPerAp)
            {
                if (newClients.Count > 0 || disconnectedClients.Count > 0)
                {
                    await _publisher.PublishClientsAsync(ap, newClients, disconnectedClients);
                }
            }
        }
        else
        {
            // Publish aggregated events for all APs
            var allNewClients = clientDiffPerAp.Values.SelectMany(x => x.newClients).ToList();
            var allDisconnectedClients = clientDiffPerAp.Values.SelectMany(x => x.disconnectedClients).ToList();
            
            // Remove clients that reconnected (moved between APs) from disconnected list
            allDisconnectedClients = allDisconnectedClients.Except(allConnectedClients).ToList();
            
            if (allNewClients.Count > 0 || allDisconnectedClients.Count > 0)
            {
                await _publisher.PublishClientsAsync(Options.AllApsAggregate, allNewClients, allDisconnectedClients);
            }
        }
    }
}