namespace LabTracker.Unifi;

/// <summary>
/// UniFi API operations.
/// </summary>
public interface IUniFiApiClient
{
    /// <summary>
    /// All sites from the UniFi controller.
    /// </summary>
    Task<List<UniFiSite>> GetSitesAsync();

    /// <summary>
    /// Access point devices for a specific site.
    /// </summary>
    /// <param name="siteId">The site ID</param>
    Task<List<UniFiDevice>> GetApDevicesAsync(string siteId);

    /// <summary>
    /// Wireless clients for a specific site.
    /// </summary>
    /// <param name="siteId">The site ID</param>
    Task<List<UniFiClient>> GetWirelessClientsAsync(string siteId);
}