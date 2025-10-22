namespace LabTracker;

/// <summary>
/// Defines how the application should initialize its state on startup.
/// </summary>
public enum InitialState
{
    /// <summary>
    /// No initialization - start with empty state.
    /// </summary>
    None,
    
    /// <summary>
    /// Initialize state by reading current MQTT retained messages.
    /// </summary>
    MQTT,
    
    /// <summary>
    /// Initialize state by querying UniFi API directly.
    /// </summary>
    UnifiAPI,
    
    /// <summary>
    /// Initialize state by combining both MQTT retained messages and UniFi API data.
    /// </summary>
    All
}

/// <summary>
/// MQTT-specific configuration options.
/// </summary>
public class MqttOptions
{
    public string BrokerHost { get; set; } = "localhost";
    public int BrokerPort { get; set; } = 1883;
    public string TopicPrefix { get; set; } = "labtracker";
    public bool IncludeApInTopic { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool UseTls { get; set; } = false;
    public bool Retain { get; set; } = false;
    public string ConnectedPayload { get; set; } = "home";
    public string DisconnectedPayload { get; set; } = "not_home";
}

/// <summary>
/// UniFi API configuration options.
/// </summary>
public class UnifiApiOptions
{
    public bool UseHttps { get; set; } = true;
    public bool IgnoreSSLErrors { get; set; } = false;
    public string BaseUrl { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public int PageSize { get; set; } = 100;
}

/// <summary>
/// UniFi SSH connection configuration options.
/// </summary>
public class UnifiOptions
{
    public string[] AccessPoints { get; set; } = Array.Empty<string>();
    public string Username { get; set; } = "admin";
    public string PrivateKeyPath { get; set; } = string.Empty;
}

public class Options
{
    public const string SectionName = "Options";
    
    /// <summary>
    /// Constant for the aggregated AP name used when MqttIncludeApInTopic is false.
    /// </summary>
    public const string AllApsAggregate = "all_aps";
    
    /// <summary>
    /// UniFi SSH connection configuration section.
    /// </summary>
    public UnifiOptions Unifi { get; set; } = new();
    
    /// <summary>
    /// UniFi API configuration section.
    /// </summary>
    public UnifiApiOptions UnifiApi { get; set; } = new();
    
    public int DelayMs { get; set; } = 60000; // 1 minute default
    public int ConnectionTimeoutSeconds { get; set; } = 5;
    public int CommandTimeoutSeconds { get; set; } = 15;
    public int MaxIdleTimeSeconds { get; set; } = 0; // 0 == disabled
    
    /// <summary>
    /// MQTT configuration section.
    /// </summary>
    public MqttOptions Mqtt { get; set; } = new();
    
    public InitialState InitialState { get; set; } = InitialState.MQTT;
}