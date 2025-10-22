namespace LabTracker;

/// <summary>
/// Immutable struct to hold client information.
/// </summary>
/// <param name="mac">Client MAC address</param>
/// <param name="ip">Client IP address</param>
/// <param name="hostname">Client hostname</param>
/// <param name="idleTime">Client idle time in seconds</param>
public readonly struct ClientInfo(string? mac, string? ip, string? hostname, int? idleTime)
{
    public readonly string? Mac { get; } = mac;
    public readonly string? Ip { get; } = ip;
    public readonly string? Hostname { get; } = hostname;
    public readonly int? IdleTime { get; } = idleTime;

    public string DisplayName => Hostname ?? Mac ?? "Unknown";

    /// <summary>
    /// Get a unique identifier for the client.
    /// </summary>
    /// <returns>Client identifier string</returns>    
    public string GetClientId() => Mac ?? "Unknown";

    /// <summary>
    /// Check if the client is considered idle based on max idle time
    /// </summary>
    /// <param name="maxIdleSeconds">Maximum idle time in seconds</param>
    /// <returns>True if client is idle, false otherwise</returns>
    public bool IsIdle(int maxIdleSeconds) => IdleTime.HasValue && IdleTime.Value > maxIdleSeconds;

    /// <summary>
    /// Returns a string representation of the client information.
    /// </summary>
    /// <returns>Formatted string with client details</returns>
    public override string ToString()
    {
        if (Mac == null)
        {
            return "Unknown Client";
        }
        else if (!string.IsNullOrEmpty(Hostname))
        {
            return $"{Mac}({Hostname})";
        }
        else
        {
            return Mac;
        }    
    }
}