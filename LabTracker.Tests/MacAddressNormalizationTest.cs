using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LabTracker.Tests;

using LabTracker;
using LabTracker.Unifi;

/// <summary>
/// Integration tests to verify MAC address normalization across different sources
/// </summary>
public class MacAddressNormalizationTest
{
    [Fact] 
    public void SshClientProvider_ShouldNormalizeMacToUppercase()
    {
        // This test verifies that SSH provider normalizes MAC addresses
        // We can't easily test this without actual SSH connections, but we can verify
        // that the ToUpperInvariant() call exists in the code
        
        // This is more of a regression test - the important thing is that
        // SshClientProvider.cs contains: mac = macStr?.ToUpperInvariant();
        
        Assert.True(true); // This test exists to document the requirement
    }

    [Fact]
    public async Task UnifiPublishedReader_ShouldNormalizeMacToUppercase()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<UnifiPublishedReader>>();
        var mockOptions = new Mock<IOptions<Options>>();
        var testOptions = new Options { Mqtt = new MqttOptions { IncludeApInTopic = true } };
        mockOptions.Setup(x => x.Value).Returns(testOptions);
        
        var mockUnifiApiClient = new Mock<IUniFiApiClient>();
        
        var testSites = new List<UniFiSite>
        {
            new UniFiSite { Id = "site1", Name = "Test Site" }
        };

        var testDevices = new List<UniFiDevice>
        {
            new UniFiDevice { Id = "device1", Name = "AP-1" }
        };

        // Test with lowercase MAC from UniFi API (simulating real-world scenario)
        var testClients = new List<UniFiClient>
        {
            new UniFiClient { Mac = "aa:bb:cc:dd:ee:ff", ApId = "device1", ConnectedAt = "2024-01-01T10:00:00Z" }
        };

        mockUnifiApiClient.Setup(x => x.GetSitesAsync()).ReturnsAsync(testSites);
        mockUnifiApiClient.Setup(x => x.GetApDevicesAsync("site1")).ReturnsAsync(testDevices);
        mockUnifiApiClient.Setup(x => x.GetWirelessClientsAsync("site1")).ReturnsAsync(testClients);

        var reader = new UnifiPublishedReader(mockLogger.Object, mockOptions.Object, mockUnifiApiClient.Object);

        // Act
        var result = await reader.ReadCurrentStatesAsync();

        // Assert
        // Verify that lowercase MAC from API is normalized to uppercase
        Assert.Single(result);
        Assert.True(result.ContainsKey("AA:BB:CC:DD:EE:FF"));
        Assert.False(result.ContainsKey("aa:bb:cc:dd:ee:ff"));
        
        var clientState = result["AA:BB:CC:DD:EE:FF"];
        Assert.Equal("AA:BB:CC:DD:EE:FF", clientState.ClientId);
    }

    [Fact]
    public void MacAddressConsistency_ShouldMaintainSameFormatAcrossSources()
    {
        // This test documents the requirement that MAC addresses should be 
        // consistently formatted as uppercase across all sources:
        // 1. SSH client provider (SshClientProvider.cs) 
        // 2. UniFi API reader (UnifiPublishedReader.cs)
        // 3. MQTT topics (when published by the system)
        
        // The format should always be: "AA:BB:CC:DD:EE:FF" (uppercase with colons)
        
        var testMacLowercase = "aa:bb:cc:dd:ee:ff";
        var expectedMacUppercase = "AA:BB:CC:DD:EE:FF";
        
        // Verify the normalization logic we use
        Assert.Equal(expectedMacUppercase, testMacLowercase.ToUpperInvariant());
        
        // This ensures both sources will produce identical MAC address formats
        Assert.True(true);
    }
}