using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using LabTracker.Ssh;

namespace LabTracker.Tests;

public class IClientInfoProviderTest
{
    private readonly Mock<ILogger<SshClientProvider>> _mockLogger;
    private readonly Mock<IOptions<Options>> _mockOptions;
    private readonly Options _testOptions;

    public IClientInfoProviderTest()
    {
        _mockLogger = new Mock<ILogger<SshClientProvider>>();
        _mockOptions = new Mock<IOptions<Options>>();
        
        _testOptions = new Options
        {
            Unifi = new UnifiOptions
            {
                AccessPoints = ["192.168.1.100", "192.168.1.101"],
                Username = "testuser",
                PrivateKeyPath = "/test/key"
            },
            ConnectionTimeoutSeconds = 10,
            CommandTimeoutSeconds = 15,
            MaxIdleTimeSeconds = 300
        };
        
        _mockOptions.Setup(x => x.Value).Returns(_testOptions);
    }

    [Fact]
    public void SshClientProvider_ImplementsIClientInfoProvider()
    {
        // Arrange & Act
        var provider = new SshClientProvider(_mockLogger.Object, _mockOptions.Object);
        
        // Assert
        Assert.IsAssignableFrom<IClientInfoProvider>(provider);
    }

    [Fact]
    public async Task GetClientsAsync_WithConnectionException_ShouldThrowSshConnectionException()
    {
        // Arrange
        var host = "testhost";
        var provider = new SshClientProvider(_mockLogger.Object, _mockOptions.Object);
        
        // Act & Assert
        var exception = await Assert.ThrowsAsync<SshConnectionException>(
            () => provider.GetClientsAsync(host, CancellationToken.None));
        
        Assert.Contains($"Failed to connect to SSH host {host}", exception.Message);
        Assert.NotNull(exception.InnerException);
    }

    [Theory]
    [InlineData("192.168.1.100")]
    [InlineData("192.168.1.101")]
    [InlineData("test-ap.local")]
    public async Task GetClientsAsync_WithValidHost_ShouldThrowSshConnectionException(string host)
    {
        // Arrange
        var provider = new SshClientProvider(_mockLogger.Object, _mockOptions.Object);
        using var cts = new CancellationTokenSource();
        
        // Act & Assert - Should throw SshConnectionException for connection failures
        var exception = await Assert.ThrowsAsync<SshConnectionException>(
            () => provider.GetClientsAsync(host, cts.Token));
        
        Assert.Contains($"Failed to connect to SSH host {host}", exception.Message);
        Assert.NotNull(exception.InnerException);
        
        // Verify error logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"SSH connection failed for host {host}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetClientsFromMultipleHostsAsync_WithEmptyHosts_ShouldReturnEmptyDictionary()
    {
        // Arrange
        var provider = new SshClientProvider(_mockLogger.Object, _mockOptions.Object);
        var hosts = Array.Empty<string>();
        using var cts = new CancellationTokenSource();
        
        // Act
        var result = await provider.GetClientsFromMultipleHostsAsync(hosts, cts.Token);
        
        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetClientsFromMultipleHostsAsync_WithMultipleHosts_ShouldProcessInParallel()
    {
        // Arrange
        var provider = new SshClientProvider(_mockLogger.Object, _mockOptions.Object);
        var hosts = new[] { "192.168.1.100", "192.168.1.101", "192.168.1.102" };
        using var cts = new CancellationTokenSource();
        
        // Act
        var result = await provider.GetClientsFromMultipleHostsAsync(hosts, cts.Token);
        
        // Assert
        Assert.NotNull(result);
        // Should handle connection errors and return empty dictionary for invalid hosts
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetClientsFromMultipleHostsAsync_WithCancellation_ShouldHandleGracefully()
    {
        // Arrange
        var provider = new SshClientProvider(_mockLogger.Object, _mockOptions.Object);
        var hosts = new[] { "192.168.1.100", "192.168.1.101" };
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        
        // Act
        var result = await provider.GetClientsFromMultipleHostsAsync(hosts, cts.Token);
        
        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetClientsAsync_WithInvalidHost_ShouldThrowSshConnectionException(string? host)
    {
        // Arrange
        var provider = new SshClientProvider(_mockLogger.Object, _mockOptions.Object);
        
        // Act & Assert
        var exception = await Assert.ThrowsAsync<SshConnectionException>(
            () => provider.GetClientsAsync(host!, CancellationToken.None));
        
        Assert.Contains($"Failed to connect to SSH host {host ?? ""}", exception.Message);
        Assert.NotNull(exception.InnerException);
    }    [Fact]
    public void SshClientProvider_Constructor_WithValidParameters_ShouldInitialize()
    {
        // Act & Assert - Should not throw
        var provider = new SshClientProvider(_mockLogger.Object, _mockOptions.Object);
        Assert.NotNull(provider);
    }

    [Fact]
    public void SshClientProvider_Constructor_WithNullLogger_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new SshClientProvider(null!, _mockOptions.Object));
    }

    [Fact]
    public void SshClientProvider_Constructor_WithNullOptions_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new SshClientProvider(_mockLogger.Object, null!));
    }

    [Fact]
    public async Task GetClientsAsync_WithShortTimeout_ShouldThrowSshConnectionException()
    {
        // Arrange
        _testOptions.ConnectionTimeoutSeconds = 1; // Very short timeout
        var provider = new SshClientProvider(_mockLogger.Object, _mockOptions.Object);
        using var cts = new CancellationTokenSource();
        
        // Act & Assert
        var exception = await Assert.ThrowsAsync<SshConnectionException>(
            () => provider.GetClientsAsync("192.168.1.100", cts.Token));
        
        Assert.Contains("Failed to connect to SSH host 192.168.1.100", exception.Message);
        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public async Task GetClientsFromMultipleHostsAsync_WithDuplicateHosts_ShouldHandleCorrectly()
    {
        // Arrange
        var provider = new SshClientProvider(_mockLogger.Object, _mockOptions.Object);
        var hosts = new[] { "192.168.1.100", "192.168.1.100", "192.168.1.101" };
        using var cts = new CancellationTokenSource();
        
        // Act
        var result = await provider.GetClientsFromMultipleHostsAsync(hosts, cts.Token);
        
        // Assert
        Assert.NotNull(result);
        // Should handle duplicates without errors
        Assert.Empty(result); // No valid connections expected
    }
}

/// <summary>
/// Tests for IClientInfoProvider interface contract compliance
/// </summary>
public class IClientInfoProviderContractTest
{
    [Fact]
    public void IClientInfoProvider_HasRequiredMethods()
    {
        // Arrange
        var interfaceType = typeof(IClientInfoProvider);
        
        // Act & Assert
        var getClientsMethod = interfaceType.GetMethod("GetClientsAsync");
        Assert.NotNull(getClientsMethod);
        Assert.Equal(typeof(Task<(string?, List<ClientInfo>)>), getClientsMethod.ReturnType);
        
        var getMultipleClientsMethod = interfaceType.GetMethod("GetClientsFromMultipleHostsAsync");
        Assert.NotNull(getMultipleClientsMethod);
        Assert.Equal(typeof(Task<Dictionary<string, List<ClientInfo>>>), getMultipleClientsMethod.ReturnType);
    }

    [Fact]
    public void IClientInfoProvider_GetClientsAsync_HasCorrectParameters()
    {
        // Arrange
        var interfaceType = typeof(IClientInfoProvider);
        var method = interfaceType.GetMethod("GetClientsAsync");
        
        // Act
        var parameters = method!.GetParameters();
        
        // Assert
        Assert.Equal(2, parameters.Length);
        Assert.Equal("host", parameters[0].Name);
        Assert.Equal(typeof(string), parameters[0].ParameterType);
        Assert.Equal("stoppingToken", parameters[1].Name);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
    }

    [Fact]
    public void IClientInfoProvider_GetClientsFromMultipleHostsAsync_HasCorrectParameters()
    {
        // Arrange
        var interfaceType = typeof(IClientInfoProvider);
        var method = interfaceType.GetMethod("GetClientsFromMultipleHostsAsync");
        
        // Act
        var parameters = method!.GetParameters();
        
        // Assert
        Assert.Equal(2, parameters.Length);
        Assert.Equal("hosts", parameters[0].Name);
        Assert.Equal(typeof(IEnumerable<string>), parameters[0].ParameterType);
        Assert.Equal("stoppingToken", parameters[1].Name);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
    }
}

/// <summary>
/// Edge case tests for SshClientProvider implementation
/// </summary>
public class SshClientProviderEdgeCaseTest
{
    private readonly Mock<ILogger<SshClientProvider>> _mockLogger;
    private readonly Mock<IOptions<Options>> _mockOptions;
    private readonly Options _testOptions;

    public SshClientProviderEdgeCaseTest()
    {
        _mockLogger = new Mock<ILogger<SshClientProvider>>();
        _mockOptions = new Mock<IOptions<Options>>();
        
        _testOptions = new Options
        {
            Unifi = new UnifiOptions
            {
                Username = "testuser",
                PrivateKeyPath = "/nonexistent/key"
            },
            ConnectionTimeoutSeconds = 1, // Very short timeout for testing
            CommandTimeoutSeconds = 1,
            MaxIdleTimeSeconds = 0 // Disabled idle filtering
        };
        
        _mockOptions.Setup(x => x.Value).Returns(_testOptions);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetClientsAsync_WithInvalidTimeout_ShouldThrowSshConnectionException(int timeout)
    {
        // Arrange
        _testOptions.ConnectionTimeoutSeconds = timeout;
        var provider = new SshClientProvider(_mockLogger.Object, _mockOptions.Object);
        using var cts = new CancellationTokenSource();
        
        // Act & Assert
        var exception = await Assert.ThrowsAsync<SshConnectionException>(
            () => provider.GetClientsAsync("192.168.1.100", cts.Token));
        
        Assert.Contains("Failed to connect to SSH host 192.168.1.100", exception.Message);
        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public async Task GetClientsAsync_WithLongRunningOperation_ShouldThrowSshConnectionException()
    {
        // Arrange
        var provider = new SshClientProvider(_mockLogger.Object, _mockOptions.Object);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        
        // Act & Assert
        var exception = await Assert.ThrowsAsync<SshConnectionException>(
            () => provider.GetClientsAsync("192.168.1.100", cts.Token));
        
        Assert.Contains("Failed to connect to SSH host 192.168.1.100", exception.Message);
        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public async Task GetClientsAsync_WithInvalidPrivateKeyPath_ShouldThrowSshConnectionException()
    {
        // Arrange
        _testOptions.Unifi.PrivateKeyPath = "/invalid/nonexistent/key/path";
        var provider = new SshClientProvider(_mockLogger.Object, _mockOptions.Object);
        using var cts = new CancellationTokenSource();
        
        // Act & Assert
        var exception = await Assert.ThrowsAsync<SshConnectionException>(
            () => provider.GetClientsAsync("192.168.1.100", cts.Token));
        
        Assert.Contains("Failed to connect to SSH host 192.168.1.100", exception.Message);
        Assert.NotNull(exception.InnerException);
        
        // Verify error logging still occurs
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid_username")]
    [InlineData("   ")]
    public async Task GetClientsAsync_WithInvalidUsername_ShouldThrowSshConnectionException(string username)
    {
        // Arrange
        _testOptions.Unifi.Username = username;
        var provider = new SshClientProvider(_mockLogger.Object, _mockOptions.Object);
        using var cts = new CancellationTokenSource();
        
        // Act & Assert
        var exception = await Assert.ThrowsAsync<SshConnectionException>(
            () => provider.GetClientsAsync("192.168.1.100", cts.Token));
        
        Assert.Contains("Failed to connect to SSH host 192.168.1.100", exception.Message);
        Assert.NotNull(exception.InnerException);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(1000)]
    public void SshClientProvider_WithDifferentIdleTimeSettings_ShouldInitialize(int maxIdleSeconds)
    {
        // Arrange
        _testOptions.MaxIdleTimeSeconds = maxIdleSeconds;
        
        // Act & Assert - Should not throw
        var provider = new SshClientProvider(_mockLogger.Object, _mockOptions.Object);
        Assert.NotNull(provider);
    }

    [Fact]
    public async Task GetClientsFromMultipleHostsAsync_WithMixedValidInvalidHosts_ShouldHandleAll()
    {
        // Arrange
        var provider = new SshClientProvider(_mockLogger.Object, _mockOptions.Object);
        var hosts = new[] { "192.168.1.100", "invalid-host", "", "test.local" };
        using var cts = new CancellationTokenSource();
        
        // Act
        var result = await provider.GetClientsFromMultipleHostsAsync(hosts, cts.Token);
        
        // Assert
        Assert.NotNull(result);
        Assert.Empty(result); // All should fail to connect in test environment
    }
}