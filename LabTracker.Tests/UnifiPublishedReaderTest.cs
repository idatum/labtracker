using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LabTracker.Tests;

using LabTracker;
using LabTracker.Unifi;

public class UnifiPublishedReaderTest
{
    private readonly Mock<ILogger<UnifiPublishedReader>> _mockLogger;
    private readonly Mock<IOptions<Options>> _mockOptions;
    private readonly Mock<IUniFiApiClient> _mockUnifiApiClient;
    private readonly Options _testOptions;

    public UnifiPublishedReaderTest()
    {
        _mockLogger = new Mock<ILogger<UnifiPublishedReader>>();
        _mockOptions = new Mock<IOptions<Options>>();
        _testOptions = new Options
        {
            Mqtt = new MqttOptions
            {
                IncludeApInTopic = true
            },
            UnifiApi = new UnifiApiOptions
            {
                BaseUrl = "unifi.example.com",
                Key = "test-key"
            }
        };
        _mockOptions.Setup(x => x.Value).Returns(_testOptions);
        
        // Create mock UniFi API client interface
        _mockUnifiApiClient = new Mock<IUniFiApiClient>();
    }

    [Fact]
    public async Task ReadCurrentStatesAsync_WithValidSitesAndClients_ReturnsClientStates()
    {
        // Arrange
        var testSites = new List<UniFiSite>
        {
            new UniFiSite { Id = "site1", Name = "Test Site 1" },
            new UniFiSite { Id = "site2", Name = "Test Site 2" }
        };

        var testDevices = new List<UniFiDevice>
        {
            new UniFiDevice { Id = "device1", Name = "AP-1", Mac = "aa:bb:cc:dd:ee:f1" },
            new UniFiDevice { Id = "device2", Name = "AP-2", Mac = "aa:bb:cc:dd:ee:f2" }
        };

        var testClientsSite1 = new List<UniFiClient>
        {
            new UniFiClient { Mac = "11:22:33:44:55:66", ApId = "device1", ConnectedAt = "2024-01-01T10:00:00Z" },
            new UniFiClient { Mac = "77:88:99:AA:BB:CC", ApId = "device2", ConnectedAt = "2024-01-01T11:00:00Z" }
        };

        var testClientsSite2 = new List<UniFiClient>
        {
            new UniFiClient { Mac = "AA:BB:CC:DD:EE:FF", ApId = "device1", ConnectedAt = "2024-01-01T12:00:00Z" },
            new UniFiClient { Mac = "DD:EE:FF:11:22:33", ApId = "device2", ConnectedAt = "2024-01-01T13:00:00Z" }
        };

        _mockUnifiApiClient.Setup(x => x.GetSitesAsync()).ReturnsAsync(testSites);
        _mockUnifiApiClient.Setup(x => x.GetApDevicesAsync("site1")).ReturnsAsync(testDevices);
        _mockUnifiApiClient.Setup(x => x.GetApDevicesAsync("site2")).ReturnsAsync(testDevices);
        _mockUnifiApiClient.Setup(x => x.GetWirelessClientsAsync("site1")).ReturnsAsync(testClientsSite1);
        _mockUnifiApiClient.Setup(x => x.GetWirelessClientsAsync("site2")).ReturnsAsync(testClientsSite2);

        var reader = new UnifiPublishedReader(_mockLogger.Object, _mockOptions.Object, _mockUnifiApiClient.Object);

        // Act
        var result = await reader.ReadCurrentStatesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(4, result.Count); // 2 clients per site * 2 sites
        
        // Verify clients from site1
        Assert.True(result.ContainsKey("11:22:33:44:55:66"));
        var client1 = result["11:22:33:44:55:66"];
        Assert.Equal("11:22:33:44:55:66", client1.ClientId);
        Assert.Equal("AP-1", client1.ApHostname);
        Assert.True(client1.IsConnected);
        Assert.Contains("Connected to AP-1", client1.LastPayload);

        Assert.True(result.ContainsKey("77:88:99:AA:BB:CC"));
        var client2 = result["77:88:99:AA:BB:CC"];
        Assert.Equal("77:88:99:AA:BB:CC", client2.ClientId);
        Assert.Equal("AP-2", client2.ApHostname);
        Assert.True(client2.IsConnected);
        Assert.Contains("Connected to AP-2", client2.LastPayload);

        // Verify clients from site2
        Assert.True(result.ContainsKey("AA:BB:CC:DD:EE:FF"));
        var client3 = result["AA:BB:CC:DD:EE:FF"];
        Assert.Equal("AA:BB:CC:DD:EE:FF", client3.ClientId);
        Assert.Equal("AP-1", client3.ApHostname);
        Assert.True(client3.IsConnected);
        Assert.Contains("Connected to AP-1", client3.LastPayload);

        Assert.True(result.ContainsKey("DD:EE:FF:11:22:33"));
        var client4 = result["DD:EE:FF:11:22:33"];
        Assert.Equal("DD:EE:FF:11:22:33", client4.ClientId);
        Assert.Equal("AP-2", client4.ApHostname);
        Assert.True(client4.IsConnected);
        Assert.Contains("Connected to AP-2", client4.LastPayload);
    }

    [Fact]
    public async Task ReadCurrentStatesAsync_WithAggregatedAPs_UsesAllApsAggregate()
    {
        // Arrange
        _testOptions.Mqtt.IncludeApInTopic = false; // Enable aggregation

        var testSites = new List<UniFiSite>
        {
            new UniFiSite { Id = "site1", Name = "Test Site" }
        };

        var testDevices = new List<UniFiDevice>
        {
            new UniFiDevice { Id = "device1", Name = "AP-1", Mac = "aa:bb:cc:dd:ee:f1" }
        };

        var testClients = new List<UniFiClient>
        {
            new UniFiClient { Mac = "11:22:33:44:55:66", ApId = "device1", ConnectedAt = "2024-01-01T10:00:00Z" }
        };

        _mockUnifiApiClient.Setup(x => x.GetSitesAsync()).ReturnsAsync(testSites);
        _mockUnifiApiClient.Setup(x => x.GetApDevicesAsync("site1")).ReturnsAsync(testDevices);
        _mockUnifiApiClient.Setup(x => x.GetWirelessClientsAsync("site1")).ReturnsAsync(testClients);

        var reader = new UnifiPublishedReader(_mockLogger.Object, _mockOptions.Object, _mockUnifiApiClient.Object);

        // Act
        var result = await reader.ReadCurrentStatesAsync();

        // Assert
        Assert.Single(result);
        var client = result["11:22:33:44:55:66"];
        Assert.Equal(Options.AllApsAggregate, client.ApHostname);
        Assert.Contains($"Connected to {Options.AllApsAggregate}", client.LastPayload);
    }

    [Fact]
    public async Task ReadCurrentStatesAsync_WithNoSites_ReturnsEmptyDictionary()
    {
        // Arrange
        _mockUnifiApiClient.Setup(x => x.GetSitesAsync()).ReturnsAsync(new List<UniFiSite>());

        var reader = new UnifiPublishedReader(_mockLogger.Object, _mockOptions.Object, _mockUnifiApiClient.Object);

        // Act
        var result = await reader.ReadCurrentStatesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ReadCurrentStatesAsync_WithUnknownDevice_UsesApId()
    {
        // Arrange
        var testSites = new List<UniFiSite>
        {
            new UniFiSite { Id = "site1", Name = "Test Site" }
        };

        var testDevices = new List<UniFiDevice>(); // No devices

        var testClients = new List<UniFiClient>
        {
            new UniFiClient { Mac = "11:22:33:44:55:66", ApId = "unknown-device", ConnectedAt = "2024-01-01T10:00:00Z" }
        };

        _mockUnifiApiClient.Setup(x => x.GetSitesAsync()).ReturnsAsync(testSites);
        _mockUnifiApiClient.Setup(x => x.GetApDevicesAsync("site1")).ReturnsAsync(testDevices);
        _mockUnifiApiClient.Setup(x => x.GetWirelessClientsAsync("site1")).ReturnsAsync(testClients);

        var reader = new UnifiPublishedReader(_mockLogger.Object, _mockOptions.Object, _mockUnifiApiClient.Object);

        // Act
        var result = await reader.ReadCurrentStatesAsync();

        // Assert
        Assert.Single(result);
        var client = result["11:22:33:44:55:66"];
        Assert.Equal("unknown-device", client.ApHostname); // Should use ApId when device name not found
    }

    [Fact]
    public async Task ReadCurrentStatesAsync_WithApiException_HandlesGracefully()
    {
        // Arrange
        _mockUnifiApiClient.Setup(x => x.GetSitesAsync()).ThrowsAsync(new HttpRequestException("API Error"));

        var reader = new UnifiPublishedReader(_mockLogger.Object, _mockOptions.Object, _mockUnifiApiClient.Object);

        // Act
        var result = await reader.ReadCurrentStatesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ReadCurrentStatesAsync_WithSiteProcessingException_ContinuesWithOtherSites()
    {
        // Arrange
        var testSites = new List<UniFiSite>
        {
            new UniFiSite { Id = "site1", Name = "Good Site" },
            new UniFiSite { Id = "site2", Name = "Bad Site" }
        };

        var testDevices = new List<UniFiDevice>
        {
            new UniFiDevice { Id = "device1", Name = "AP-1", Mac = "aa:bb:cc:dd:ee:f1" }
        };

        var testClients = new List<UniFiClient>
        {
            new UniFiClient { Mac = "11:22:33:44:55:66", ApId = "device1", ConnectedAt = "2024-01-01T10:00:00Z" }
        };

        _mockUnifiApiClient.Setup(x => x.GetSitesAsync()).ReturnsAsync(testSites);
        _mockUnifiApiClient.Setup(x => x.GetApDevicesAsync("site1")).ReturnsAsync(testDevices);
        _mockUnifiApiClient.Setup(x => x.GetApDevicesAsync("site2")).ThrowsAsync(new Exception("Site error"));
        _mockUnifiApiClient.Setup(x => x.GetWirelessClientsAsync("site1")).ReturnsAsync(testClients);

        var reader = new UnifiPublishedReader(_mockLogger.Object, _mockOptions.Object, _mockUnifiApiClient.Object);

        // Act
        var result = await reader.ReadCurrentStatesAsync();

        // Assert
        Assert.Single(result); // Should have one client from the good site
        Assert.True(result.ContainsKey("11:22:33:44:55:66"));
    }

    [Fact]
    public async Task ReadCurrentStatesAsync_WithLowercaseMacAddresses_NormalizesToUppercase()
    {
        // Arrange
        var testSites = new List<UniFiSite>
        {
            new UniFiSite { Id = "site1", Name = "Test Site" }
        };

        var testDevices = new List<UniFiDevice>
        {
            new UniFiDevice { Id = "device1", Name = "AP-1", Mac = "aa:bb:cc:dd:ee:f1" }
        };

        // Test with lowercase MAC addresses from UniFi API
        var testClients = new List<UniFiClient>
        {
            new UniFiClient { Mac = "aa:bb:cc:dd:ee:ff", ApId = "device1", ConnectedAt = "2024-01-01T10:00:00Z" }
        };

        _mockUnifiApiClient.Setup(x => x.GetSitesAsync()).ReturnsAsync(testSites);
        _mockUnifiApiClient.Setup(x => x.GetApDevicesAsync("site1")).ReturnsAsync(testDevices);
        _mockUnifiApiClient.Setup(x => x.GetWirelessClientsAsync("site1")).ReturnsAsync(testClients);

        var reader = new UnifiPublishedReader(_mockLogger.Object, _mockOptions.Object, _mockUnifiApiClient.Object);

        // Act
        var result = await reader.ReadCurrentStatesAsync();

        // Assert
        Assert.Single(result);
        // Should be normalized to uppercase
        Assert.True(result.ContainsKey("AA:BB:CC:DD:EE:FF"));
        Assert.False(result.ContainsKey("aa:bb:cc:dd:ee:ff")); // Should not contain lowercase version
        
        var client = result["AA:BB:CC:DD:EE:FF"];
        Assert.Equal("AA:BB:CC:DD:EE:FF", client.ClientId); // ClientId should also be uppercase
    }
}

// Test for the IPublished interface contract compliance
public class UnifiPublishedReaderContractTest
{
    [Fact]
    public void UnifiPublishedReader_ShouldImplementIPublished()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<UnifiPublishedReader>>();
        var mockOptions = new Mock<IOptions<Options>>();
        var testOptions = new Options();
        mockOptions.Setup(x => x.Value).Returns(testOptions);
        
        var mockUnifiApiClient = new Mock<IUniFiApiClient>();

        // Act & Assert
        var reader = new UnifiPublishedReader(mockLogger.Object, mockOptions.Object, mockUnifiApiClient.Object);
        Assert.IsAssignableFrom<IPublished>(reader);
    }
}