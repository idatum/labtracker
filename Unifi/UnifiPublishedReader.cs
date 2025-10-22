using Microsoft.Extensions.Options;

namespace LabTracker.Unifi;

/// <summary>
/// UniFi API implementation that reads current published client states from UniFi controllers.
/// </summary>
public class UnifiPublishedReader : IPublished
{
    private readonly ILogger<UnifiPublishedReader> _logger;
    private readonly Options _options;
    private readonly IUniFiApiClient _unifiApiClient;

    public bool ForceSnapshot => true;

    public UnifiPublishedReader(ILogger<UnifiPublishedReader> logger, IOptions<Options> options, IUniFiApiClient unifiApiClient)
    {
        _logger = logger;
        _options = options.Value;
        _unifiApiClient = unifiApiClient;
    }

    public async Task<Dictionary<string, ClientState>> ReadCurrentStatesAsync()
    {
        var clientStates = new Dictionary<string, ClientState>();

        try
        {
            _logger.LogInformation("Reading current WiFi client states from UniFi API");

            // Get all sites from the UniFi controller
            var sites = await _unifiApiClient.GetSitesAsync();
            if (sites.Count == 0)
            {
                _logger.LogWarning("No UniFi sites found");
                return clientStates;
            }

            _logger.LogDebug("Found {SiteCount} UniFi sites", sites.Count);

            // Process each site
            foreach (var site in sites)
            {
                try
                {
                    // Get access points for this site to map device IDs to hostnames
                    var devices = await _unifiApiClient.GetApDevicesAsync(site.Id);
                    var deviceMap = devices.ToDictionary(d => d.Id, d => d.Name);

                    // Get wireless clients for this site
                    var clients = await _unifiApiClient.GetWirelessClientsAsync(site.Id);

                    _logger.LogDebug("Site {SiteName}: Found {ClientCount} wireless clients", site.Name, clients.Count);

                    foreach (var client in clients)
                    {
                        var apHostname = _options.Mqtt.IncludeApInTopic
                            ? deviceMap.TryGetValue(client.ApId, out var deviceName)
                                ? deviceName
                                : client.ApId
                            : Options.AllApsAggregate;

                        // Ensure MAC address is always uppercase for consistency
                        var macAddress = client.Mac.ToUpperInvariant();

                        var clientState = new ClientState
                        {
                            ClientId = macAddress,
                            ApHostname = apHostname,
                            IsConnected = true,
                            LastUpdated = DateTime.UtcNow,
                            LastPayload = $"Connected to {apHostname} at {client.ConnectedAt}"
                        };

                        clientStates[PublishedUtils.CreateClientStateKey(clientState.ClientId, apHostname)] = clientState;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing UniFi site {SiteName} ({SiteId})", site.Name, site.Id);
                }
            }

            _logger.LogInformation("Successfully read {ClientCount} WiFi client states from UniFi API", clientStates.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading WiFi client states from UniFi API");
        }

        return clientStates;
    }
}