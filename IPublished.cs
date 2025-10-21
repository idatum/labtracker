namespace LabTracker;

/// <summary>
/// Interface for reading current published MQTT messages to initialize client state.
/// </summary>
public interface IPublished
{
    /// <summary>
    /// Indicates whether this implementation forces a complete snapshot of all connected clients.
    /// </summary>
    bool ForceSnapshot { get; }

    /// <summary>
    /// Read current published client states from MQTT retained messages.
    /// </summary>
    /// <returns>Dictionary with client ID as key and connection state as value</returns>
    Task<Dictionary<string, ClientState>> ReadCurrentStatesAsync();
}

/// <summary>
/// Represents the current state of a client from published MQTT messages.
/// </summary>
public class ClientState
{
    public string ClientId { get; set; } = string.Empty;
    public string? ApHostname { get; set; }
    public bool IsConnected { get; set; }
    public DateTime LastUpdated { get; set; }
    public string? LastPayload { get; set; }
}