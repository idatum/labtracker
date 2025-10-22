using LabTracker.Mqtt;
using LabTracker.Unifi;
using Microsoft.Extensions.Options;

namespace LabTracker;

/// <summary>
/// Combined implementation that reads client states from both MQTT retained messages and UniFi API.
/// </summary>
public class CombinedPublishedReader : IPublished
{
    private readonly ILogger<CombinedPublishedReader> _logger;
    private readonly MqttPublishedReader _mqttReader;
    private readonly UnifiPublishedReader _unifiReader;

    /// <summary>
    /// Gets a value indicating whether this implementation forces a complete snapshot.
    /// </summary>
    public bool ForceSnapshot => true;

    /// <summary>
    /// Initializes a new instance of the CombinedPublishedReader class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output</param>
    /// <param name="options">Configuration options for both MQTT and UniFi connections</param>
    /// <param name="unifiApiClient">UniFi API client for retrieving device information</param>
    /// <param name="loggerFactory">Logger factory for creating individual component loggers</param>
    public CombinedPublishedReader(ILogger<CombinedPublishedReader> logger, IOptions<Options> options, IUniFiApiClient unifiApiClient, ILoggerFactory loggerFactory)
    {
        _logger = logger;
        
        // Create individual readers with proper dependency injection
        var mqttLogger = loggerFactory.CreateLogger<MqttPublishedReader>();
        _mqttReader = new MqttPublishedReader(mqttLogger, options);
        
        var unifiLogger = loggerFactory.CreateLogger<UnifiPublishedReader>();
        _unifiReader = new UnifiPublishedReader(unifiLogger, options, unifiApiClient);
    }

    /// <summary>
    /// Reads current client states.
    /// </summary>
    /// <returns>A dictionary with combined client state keys and their connection states</returns>
    public async Task<Dictionary<string, ClientState>> ReadCurrentStatesAsync()
    {
        var combinedStates = new Dictionary<string, ClientState>();
        try
        {
            // UniFiApi state
            _logger.LogDebug("Reading client states from UniFi API");
            var unifiStates = await _unifiReader.ReadCurrentStatesAsync();
            _logger.LogInformation("Found {unifiCount} client states from UniFi API", unifiStates.Count);

            // Combined result
            foreach (var (key, state) in unifiStates)
            {
                _logger.LogDebug("UnifiApi: {key}", key);
                combinedStates[key] = state;
            }

            // MQTT state
            _logger.LogDebug("Reading client states from MQTT retained messages");
            var mqttStates = await _mqttReader.ReadCurrentStatesAsync();
            _logger.LogInformation("Found {mqttCount} client states from MQTT", mqttStates.Count);

            // Merge state
            foreach (var (key, state) in mqttStates)
            {
                _logger.LogDebug("MQTT: {key}", key);
                if (combinedStates.ContainsKey(key))
                {
                    _logger.LogDebug("Overriding state for {clientId}", state.ClientId);
                }
                combinedStates[key] = state;
            }

            _logger.LogInformation("Merged total: {totalCount} client states (MQTT: {mqttCount}, UniFi: {unifiCount})", 
                combinedStates.Count, mqttStates.Count, unifiStates.Count);

            return combinedStates;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read combined client states from MQTT and UniFi API");
            
            // Fallback: try to return at least MQTT data if available
            try
            {
                _logger.LogWarning("Attempting to fallback to MQTT data only");
                var mqttFallback = await _mqttReader.ReadCurrentStatesAsync();
                _logger.LogInformation("Fallback successful: returning {count} states from MQTT only", mqttFallback.Count);
                return mqttFallback;
            }
            catch (Exception fallbackEx)
            {
                _logger.LogError(fallbackEx, "Fallback to MQTT also failed, returning empty state");
                return new Dictionary<string, ClientState>();
            }
        }
    }
}