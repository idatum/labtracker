namespace LabTracker;

/// <summary>
/// Client information provider.
/// </summary>
public interface IClientInfoProvider
{
    /// <summary>
    /// Client information for a device.
    /// </summary>
    /// <param name="host">IP address or hostname of the target device</param>
    /// <param name="stoppingToken">Cancellation token to abort the operation</param>
    /// <returns>Tuple containing the device hostname and list of connected clients</returns>
    Task<(string? hostname, List<ClientInfo> clients)> GetClientsAsync(string host, CancellationToken stoppingToken);
}