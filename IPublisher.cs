namespace LabTracker;

/// <summary>
/// Publish client presence data
/// </summary>
public interface IPublisher
{
    /// <summary>
    /// Initialize the publisher connection
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Publish client connection events
    /// </summary>
    /// <param name="apHostname">The access point hostname</param>
    /// <param name="connectedClients">List of newly connected clients</param>
    /// <param name="disconnectedClients">List of disconnected clients</param>
    Task PublishClientsAsync(string apHostname, List<string> connectedClients, List<string> disconnectedClients);

    /// <summary>
    /// Whether publisher is connected and ready
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Dispose
    /// </summary>
    ValueTask DisposeAsync();
}