using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace LabTracker;

public class UniFiApiClient : IUniFiApiClient
{
    private readonly HttpClient _httpClient;
    private readonly Options _config;
    private readonly ILogger<UniFiApiClient> _logger;

    public UniFiApiClient(IOptions<Options> config, ILogger<UniFiApiClient> logger)
    {
        _config = config.Value;
        _logger = logger;

        // Log configuration source information
        var hasEnvVars = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("UNIFINGER_UNIFI__BASEURL"));
        var configSource = hasEnvVars ? "environment variables" : "configuration file";
        _logger.LogDebug("UniFi configuration loaded from {ConfigSource}", configSource);

        var handler = new HttpClientHandler();

        if (_config.UnifiApi.UseHttps && _config.UnifiApi.IgnoreSSLErrors)
        {
            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
        }

        _httpClient = new HttpClient(handler);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "LabTracker/1.0");
        _httpClient.DefaultRequestHeaders.Add("X-API-Key", _config.UnifiApi.Key);
    }

    private string GetBaseUrl()
    {
        var protocol = _config.UnifiApi.UseHttps ? "https" : "http";
        var baseUrl = _config.UnifiApi.BaseUrl;
        if (baseUrl.StartsWith("https://"))
            baseUrl = baseUrl.Substring(8);
        else if (baseUrl.StartsWith("http://"))
            baseUrl = baseUrl.Substring(7);
            
        return $"{protocol}://{baseUrl}";
    }

    public async Task<List<UniFiSite>> GetSitesAsync()
    {
        try
        {
            var allSites = new List<UniFiSite>();
            var offset = 0;
            var limit = _config.UnifiApi.PageSize;
            
            while (true)
            {
                var sitesUrl = $"{GetBaseUrl()}/proxy/network/integration/v1/sites?limit={limit}&offset={offset}";
                _logger.LogDebug("Fetching UniFi sites from {SitesUrl}", sitesUrl);
                var response = await _httpClient.GetAsync(sitesUrl);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var sitesResponse = JsonSerializer.Deserialize<UniFiSitesResponse>(content);
                    var sites = sitesResponse?.Data ?? [];
                    
                    if (sites.Count == 0)
                        break;
                        
                    allSites.AddRange(sites);
                    
                    if (sites.Count < limit)
                        break;
                        
                    offset += limit;
                }
                else
                {
                    _logger.LogWarning("No unifi sites. Status: {StatusCode}", response.StatusCode);
                    break;
                }
            }
            
            return allSites;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting UniFi sites");
            return [];
        }
    }

    public async Task<List<UniFiDevice>> GetApDevicesAsync(string siteId)
    {
        try
        {
            var allDevices = new List<UniFiDevice>();
            var offset = 0;
            var limit = _config.UnifiApi.PageSize;
            
            while (true)
            {
                var response = await _httpClient.GetAsync($"{GetBaseUrl()}/proxy/network/integration/v1/sites/{siteId}/devices?limit={limit}&offset={offset}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var devicesResponse = JsonSerializer.Deserialize<UniFiDevicesResponse>(content);
                    var devices = devicesResponse?.Data ?? [];
                    
                    if (devices.Count == 0)
                        break;
                        
                    allDevices.AddRange(devices);
                    
                    if (devices.Count < limit)
                        break;
                        
                    offset += limit;
                }
                else
                {
                    _logger.LogWarning("No devices for site {SiteId}. Status: {StatusCode}", siteId, response.StatusCode);
                    break;
                }
            }
            
            return allDevices;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting UniFi devices for site {siteId}", siteId);
            return [];
        }
    }

    public async Task<List<UniFiClient>> GetWirelessClientsAsync(string siteId)
    {
        try
        {
            var allClients = new List<UniFiClient>();
            var offset = 0;
            var limit = _config.UnifiApi.PageSize;
            var filter = WebUtility.UrlEncode("type.eq('WIRELESS')");
            
            while (true)
            {
                var response = await _httpClient.GetAsync($"{GetBaseUrl()}/proxy/network/integration/v1/sites/{siteId}/clients?filter={filter}&limit={limit}&offset={offset}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var clientsResponse = JsonSerializer.Deserialize<UniFiClientsResponse>(content);
                    var clients = clientsResponse?.Data ?? [];
                    
                    if (clients.Count == 0)
                        break;
                        
                    allClients.AddRange(clients);
                    
                    if (clients.Count < limit)
                        break;
                        
                    offset += limit;
                }
                else
                {
                    _logger.LogWarning("No wireless clients for site {SiteId}. Status: {StatusCode}", siteId, response.StatusCode);
                    break;
                }
            }
            
            return allClients;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting UniFi clients for site {SiteId}", siteId);
            return [];
        }
    }
}