using System.Text.Json;
using Microsoft.Extensions.Options;
using Renci.SshNet;

namespace LabTracker;

/// <summary>
/// SSH-based implementation of IClientInfoProvider for retrieving client information from UniFi Access Points.
/// </summary>
public class SshClientProvider : IClientInfoProvider
{
    private readonly ILogger<SshClientProvider> _logger;
    private readonly Options _options;

    public SshClientProvider(ILogger<SshClientProvider> logger, IOptions<Options> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Retrieves client information from a specific host via SSH.
    /// </summary>
    /// <param name="host">IP address or hostname of the target UniFi AP</param>
    /// <param name="stoppingToken">Cancellation token to abort the operation</param>
    /// <returns>Tuple containing the device hostname and list of connected clients</returns>
    public async Task<(string? hostname, List<ClientInfo> clients)> GetClientsAsync(string host, CancellationToken stoppingToken)
    {
        try
        {
            var hostData = await ExecuteSshCommand(host, "mca-dump", stoppingToken);
            var (hostname, clients) = ParseHostData(hostData, host);
            return (hostname, clients);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SSH connection failed for host {host}", host);
            // Re-throw as SshConnectionException to allow Worker to detect SSH failures
            throw new SshConnectionException($"Failed to connect to SSH host {host}", ex);
        }
    }

    /// <summary>
    /// Retrieves client information from multiple hosts in parallel.
    /// </summary>
    /// <param name="hosts">Collection of IP addresses or hostnames of target devices</param>
    /// <param name="stoppingToken">Cancellation token to abort the operation</param>
    /// <returns>Dictionary with device hostname as key and list of connected clients as value</returns>
    public async Task<Dictionary<string, List<ClientInfo>>> GetClientsFromMultipleHostsAsync(IEnumerable<string> hosts, CancellationToken stoppingToken)
    {
        var tasks = hosts.Select(async host =>
        {
            try
            {
                var result = await GetClientsAsync(host, stoppingToken);
                return new { Host = host, Result = result, Success = true };
            }
            catch (SshConnectionException ex)
            {
                _logger.LogError(ex, "SSH connection failed for host {host} during multi-host operation", host);
                // Return empty result for failed connections to maintain compatibility
                return new { Host = host, Result = (hostname: (string?)null, clients: new List<ClientInfo>()), Success = false };
            }
        });

        var results = await Task.WhenAll(tasks);
        var clientsPerHost = new Dictionary<string, List<ClientInfo>>();

        foreach (var item in results)
        {
            var (hostname, clients) = item.Result;
            if (!string.IsNullOrEmpty(hostname))
            {
                clientsPerHost[hostname] = clients;
            }
        }

        return clientsPerHost;
    }

    /// <summary>
    /// Executes an SSH command on the specified UniFi Access Point.
    /// </summary>
    /// <param name="sshHost">IP address or hostname of the target AP</param>
    /// <param name="command">SSH command to execute (typically 'mca-dump')</param>
    /// <param name="stoppingToken">Cancellation token to abort the operation</param>
    /// <returns>String output from the executed command</returns>
    private async Task<string> ExecuteSshCommand(string sshHost, string command, CancellationToken stoppingToken)
    {
        _logger.LogDebug("Connecting to host {host}", sshHost);
        
        var connectionInfo = CreateConnectionInfo(sshHost);
        
        using var client = new SshClient(connectionInfo);
        await client.ConnectAsync(stoppingToken);
        _logger.LogDebug("Connected to {host}", sshHost);
        
        try
        {
            var cmd = client.CreateCommand(command);
            cmd.CommandTimeout = TimeSpan.FromSeconds(_options.CommandTimeoutSeconds);
            var result = cmd.Execute();
            _logger.LogDebug("Command '{command}' executed successfully on {host}", command, sshHost);
            return result;
        }
        finally
        {
            client.Disconnect();
        }
    }

    /// <summary>
    /// Creates SSH connection configuration for connecting to UniFi Access Points.
    /// </summary>
    /// <param name="sshHost">Target host IP address or hostname</param>
    /// <returns>Configured ConnectionInfo object for SSH client</returns>
    private ConnectionInfo CreateConnectionInfo(string sshHost)
    {
        return new ConnectionInfo(sshHost, _options.Unifi.Username, 
            new PrivateKeyAuthenticationMethod(_options.Unifi.Username, new PrivateKeyFile(_options.Unifi.PrivateKeyPath)))
        {
            Timeout = TimeSpan.FromSeconds(_options.ConnectionTimeoutSeconds)
        };
    }

    /// <summary>
    /// Parses the raw mca-dump JSON data to process the vap_table.
    /// </summary>
    /// <param name="hostData">Raw JSON string from mca-dump command</param>
    /// <param name="sshHost">Original SSH host (used as fallback hostname)</param>
    /// <returns>Tuple containing the parsed hostname and list of connected clients</returns>
    private (string hostname, List<ClientInfo> clients) ParseHostData(string hostData, string sshHost)
    {
        using var mcaDocument = JsonDocument.Parse(hostData);
        var rootElement = mcaDocument.RootElement;
        
        var hostname = rootElement.GetProperty("hostname").GetString() ?? sshHost;
        _logger.LogDebug("SSH hostname of {host} is AP {hostname}", sshHost, hostname);
        
        if (rootElement.TryGetProperty("vap_table", out JsonElement vapTable) && 
            vapTable.ValueKind == JsonValueKind.Array)
        {
            var clients = ProcessVapTable(vapTable, hostname);
            return (hostname, clients);
        }
        
        _logger.LogError("No vap_table found in response for {hostname}", hostname);
        throw new SshConnectionException($"Invalid response from SSH host {hostname}: no vap_table found");
    }

    /// <summary>
    /// Processes the vap_table JSON array to extract connected client information.
    /// </summary>
    /// <param name="vapTable">JSON element representing the vap_table array</param>
    /// <param name="hostname">hostname of the Access Point</param>
    /// <returns>List of connected ClientInfo objects</returns>
    private List<ClientInfo> ProcessVapTable(JsonElement vapTable, string hostname)
    {
        var clients = new List<ClientInfo>();
        var vapIndex = 0;
        foreach (var vapElement in vapTable.EnumerateArray())
        {
            _logger.LogDebug("Processing vap_table index {vapIndex}", vapIndex);

            // Check if this vap element has a sta_table property
            if (vapElement.TryGetProperty("sta_table", out JsonElement staTable) &&
                staTable.ValueKind == JsonValueKind.Array &&
                staTable.GetArrayLength() > 0)
            {
                _logger.LogDebug("Found non-empty sta_table in vap_table index {vapIndex} from {hostname} with {count} entries",
                    vapIndex, hostname, staTable.GetArrayLength());

                foreach (var staEntry in staTable.EnumerateArray())
                {
                    // Filter to include only specified properties
                    var clientInfo = FilterJsonProperties(staEntry, ["mac", "ip", "hostname", "idletime"]);
                    if (_options.MaxIdleTimeSeconds > 0 && clientInfo.IsIdle(_options.MaxIdleTimeSeconds))
                    {
                        _logger.LogDebug("Skipping client {client} due to idle time {idletime} seconds exceeding max of {maxIdleTimeSeconds}",
                            clientInfo.DisplayName, clientInfo.IdleTime!.Value, _options.MaxIdleTimeSeconds);
                        continue;
                    }
                    // Store the client in the dictionary
                    clients.Add(clientInfo);
                }
                _logger.LogDebug("Stored {totalClients} clients for hostname {hostname}", clients.Count, hostname);
            }
            else
            {
                _logger.LogDebug("vap_table index {vapIndex} has no sta_table or is empty", vapIndex);
            }
            ++vapIndex;
        }
        return clients;
    }

    /// <summary>
    /// Filters a JSON element to extract only the specified properties into a ClientInfo object.
    /// </summary>
    /// <param name="element">JSON element representing a client entry</param>
    /// <param name="propertiesToInclude">Array of property names to include in the ClientInfo</param>
    /// <returns>ClientInfo object with the filtered properties</returns>
    private static ClientInfo FilterJsonProperties(JsonElement element, string[] propertiesToInclude)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            string? mac = null;
            string? ip = null;
            string? hostname = null;
            int? idleTime = null;

            foreach (var property in element.EnumerateObject())
            {
                if (propertiesToInclude.Contains(property.Name))
                {
                    switch (property.Name)
                    {
                        case "mac":
                            var macStr = property.Value.GetString();
                            mac = macStr?.ToUpperInvariant();
                            break;
                        case "ip":
                            ip = property.Value.GetString();
                            break;
                        case "hostname":
                            hostname = property.Value.GetString();
                            break;
                        case "idletime":
                            if (property.Value.TryGetInt32(out var idleValue))
                                idleTime = idleValue;
                            break;
                    }
                }
            }

            return new ClientInfo(mac, ip, hostname, idleTime);
        }
        return new ClientInfo(null, null, null, null);
    }
}