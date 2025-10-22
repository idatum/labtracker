using Xunit;

namespace LabTracker.Tests;

public class ClientInfoTest
{
    [Fact]
    public void GetClientId_WhenBothMacAndHostnameNull_ShouldReturnUnknown()
    {
        // Arrange
        var clientInfo = new ClientInfo(null, "192.168.1.100", null, 30);
        
        // Act
        var result = clientInfo.GetClientId();
        
        // Assert
        Assert.Equal("Unknown", result);
    }
    
    [Fact]
    public void IsIdle_WhenIdleTimeGreaterThanMax_ShouldReturnTrue()
    {
        // Arrange
        var clientInfo = new ClientInfo("AA:BB:CC:DD:EE:FF", "192.168.1.100", "laptop", 600);
        
        // Act
        var result = clientInfo.IsIdle(maxIdleSeconds: 300);
        
        // Assert
        Assert.True(result);
    }
    
    [Fact]
    public void IsIdle_WhenIdleTimeLessThanMax_ShouldReturnFalse()
    {
        // Arrange
        var clientInfo = new ClientInfo("AA:BB:CC:DD:EE:FF", "192.168.1.100", "laptop", 150);
        
        // Act
        var result = clientInfo.IsIdle(maxIdleSeconds: 300);
        
        // Assert
        Assert.False(result);
    }
    
    [Fact]
    public void IsIdle_WhenIdleTimeIsNull_ShouldReturnFalse()
    {
        // Arrange
        var clientInfo = new ClientInfo("AA:BB:CC:DD:EE:FF", "192.168.1.100", "laptop", null);
        
        // Act
        var result = clientInfo.IsIdle(maxIdleSeconds: 300);
        
        // Assert
        Assert.False(result);
    }
    
    [Fact]
    public void DisplayName_WhenHostnameExists_ShouldReturnHostname()
    {
        // Arrange
        var clientInfo = new ClientInfo("AA:BB:CC:DD:EE:FF", "192.168.1.100", "laptop", 30);
        
        // Act
        var result = clientInfo.DisplayName;
        
        // Assert
        Assert.Equal("laptop", result);
    }
    
    [Fact]
    public void DisplayName_WhenHostnameNullButMacExists_ShouldReturnMac()
    {
        // Arrange
        var clientInfo = new ClientInfo("AA:BB:CC:DD:EE:FF", "192.168.1.100", null, 30);
        
        // Act
        var result = clientInfo.DisplayName;
        
        // Assert
        Assert.Equal("AA:BB:CC:DD:EE:FF", result);
    }
    
    [Fact]
    public void DisplayName_WhenBothHostnameAndMacNull_ShouldReturnUnknown()
    {
        // Arrange
        var clientInfo = new ClientInfo(null, "192.168.1.100", null, 30);
        
        // Act
        var result = clientInfo.DisplayName;
        
        // Assert
        Assert.Equal("Unknown", result);
    }
    
    [Fact]
    public void ToString_WhenHostnameExists_ShouldFormatWithHostname()
    {
        // Arrange
        var clientInfo = new ClientInfo("AA:BB:CC:DD:EE:FF", "192.168.1.100", "laptop", 30);
        
        // Act
        var result = clientInfo.ToString();
        
        // Assert
        Assert.Equal("AA:BB:CC:DD:EE:FF(laptop)", result);
    }
    
    [Fact]
    public void ToString_WhenHostnameNullButMacExists_ShouldReturnMacOnly()
    {
        // Arrange
        var clientInfo = new ClientInfo("AA:BB:CC:DD:EE:FF", "192.168.1.100", null, 30);
        
        // Act
        var result = clientInfo.ToString();
        
        // Assert
        Assert.Equal("AA:BB:CC:DD:EE:FF", result);
    }
    
    [Fact]
    public void ToString_WhenMacIsNull_ShouldReturnUnknownClient()
    {
        // Arrange
        var clientInfo = new ClientInfo(null, "192.168.1.100", "laptop", 30);
        
        // Act
        var result = clientInfo.ToString();
        
        // Assert
        Assert.Equal("Unknown Client", result);
    }
    
    [Fact]
    public void Constructor_ShouldSetAllProperties()
    {
        // Arrange & Act
        var clientInfo = new ClientInfo("AA:BB:CC:DD:EE:FF", "192.168.1.100", "laptop", 30);
        
        // Assert
        Assert.Equal("AA:BB:CC:DD:EE:FF", clientInfo.Mac);
        Assert.Equal("192.168.1.100", clientInfo.Ip);
        Assert.Equal("laptop", clientInfo.Hostname);
        Assert.Equal(30, clientInfo.IdleTime);
    }
    
    [Fact]
    public void IsIdle_WhenIdleTimeEqualsMax_ShouldReturnFalse()
    {
        // Arrange
        var clientInfo = new ClientInfo("AA:BB:CC:DD:EE:FF", "192.168.1.100", "laptop", 300);
        
        // Act
        var result = clientInfo.IsIdle(maxIdleSeconds: 300);
        
        // Assert
        Assert.False(result);
    }
}
