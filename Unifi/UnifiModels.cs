using System.Text.Json.Serialization;

namespace LabTracker.Unifi;

public class UniFiSitesResponse
{
    [JsonPropertyName("data")]
    public List<UniFiSite> Data { get; set; } = new();
}

public class UniFiSite
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("desc")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}

public class UniFiDevicesResponse
{
    [JsonPropertyName("data")]
    public List<UniFiDevice> Data { get; set; } = [];
}

public class UniFiDevice
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("macAddress")]
    public string Mac { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;
}

public class UniFiClientsResponse
{
    [JsonPropertyName("data")]
    public List<UniFiClient> Data { get; set; } = [];
}

public class UniFiClient
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("connectedAt")]
    public string ConnectedAt { get; set; } = string.Empty;

    [JsonPropertyName("macAddress")]
    public string Mac { get; set; } = string.Empty;

    [JsonPropertyName("uplinkDeviceId")]
    public string ApId { get; set; } = string.Empty;
}