namespace LabTracker;

/// <summary>
/// Interface for UniFi API operations
/// </summary>
public interface IUniFiApiClient
{
    /// <summary>
    /// Get all sites from the UniFi controller
    /// </summary>
    Task<List<UniFiSite>> GetSitesAsync();

    /// <summary>
    /// Get access point devices for a specific site
    /// </summary>
    /// <param name="siteId">The site ID</param>
    Task<List<UniFiDevice>> GetApDevicesAsync(string siteId);

    /// <summary>
    /// Get wireless clients for a specific site
    /// </summary>
    /// <param name="siteId">The site ID</param>
    Task<List<UniFiClient>> GetWirelessClientsAsync(string siteId);
}