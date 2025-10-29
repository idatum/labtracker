namespace LabTracker;

/// <summary>
/// Interface for retrieving client information from network devices.
/// </summary>
public interface IClientInfoProvider
{
    /// <summary>
    /// Retrieves client information from a specific host.
    /// </summary>
    /// <param name="host">IP address or hostname of the target device</param>
    /// <param name="stoppingToken">Cancellation token to abort the operation</param>
    /// <returns>Tuple containing the device hostname and list of connected clients</returns>
    Task<(string? hostname, List<ClientInfo> clients)> GetClientsAsync(string host, CancellationToken stoppingToken);
}