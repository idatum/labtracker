using LabTracker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LabTracker.Tests;

public class ConsolePublisherTest
{
    private readonly Mock<ILogger<ConsolePublisher>> _mockLogger;
    private readonly ConsolePublisher _publisher;

    public ConsolePublisherTest()
    {
        _mockLogger = new Mock<ILogger<ConsolePublisher>>();
        _publisher = new ConsolePublisher(_mockLogger.Object);
    }

    [Fact]
    public void IsConnected_ShouldAlwaysReturnTrue()
    {
        // Act
        var result = _publisher.IsConnected;
        
        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task InitializeAsync_ShouldLogInitialization()
    {
        // Act
        await _publisher.InitializeAsync();
        
        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Console publisher initialized")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishClientsAsync_WithConnectedClients_ShouldLogDebugMessage()
    {
        // Arrange
        var apHostname = "test-ap";
        var connectedClients = new List<string> { "client1", "client2" };
        var disconnectedClients = new List<string>();

        // Act
        await _publisher.PublishClientsAsync(apHostname, connectedClients, disconnectedClients);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Published client events for AP test-ap to console")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishClientsAsync_WithDisconnectedClients_ShouldLogDebugMessage()
    {
        // Arrange
        var apHostname = "test-ap";
        var connectedClients = new List<string>();
        var disconnectedClients = new List<string> { "client3", "client4" };

        // Act
        await _publisher.PublishClientsAsync(apHostname, connectedClients, disconnectedClients);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Published client events for AP test-ap to console")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishClientsAsync_WithEmptyLists_ShouldStillLogDebugMessage()
    {
        // Arrange
        var apHostname = "test-ap";
        var connectedClients = new List<string>();
        var disconnectedClients = new List<string>();

        // Act
        await _publisher.PublishClientsAsync(apHostname, connectedClients, disconnectedClients);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Published client events for AP test-ap to console")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_ShouldLogDisposal()
    {
        // Act
        await _publisher.DisposeAsync();

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Console publisher disposed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}

public class MqttPublisherTest
{
    private readonly Mock<ILogger<MqttPublisher>> _mockLogger;
    private readonly Mock<IOptions<Options>> _mockOptions;
    private readonly Options _options;

    public MqttPublisherTest()
    {
        _mockLogger = new Mock<ILogger<MqttPublisher>>();
        _options = new Options
        {
            Mqtt = new MqttOptions
            {
                BrokerHost = "test-host",
                BrokerPort = 8883,
                Username = "test-user",
                Password = "test-pass",
                TopicPrefix = "test/topic",
                IncludeApInTopic = true
            }
        };
        _mockOptions = new Mock<IOptions<Options>>();
        _mockOptions.Setup(x => x.Value).Returns(_options);
    }

    [Fact]
    public void IsConnected_WhenMqttClientIsNull_ShouldReturnFalse()
    {
        // Arrange
        var publisher = new MqttPublisher(_mockLogger.Object, _mockOptions.Object);

        // Act
        var result = publisher.IsConnected;

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Constructor_ShouldCreateMqttPublisher()
    {
        // Arrange & Act
        var publisher = new MqttPublisher(_mockLogger.Object, _mockOptions.Object);

        // Assert
        Assert.NotNull(publisher);
        Assert.False(publisher.IsConnected); // Should be false before connection
    }

    [Fact]
    public async Task DisposeAsync_ShouldDisposeResources()
    {
        // Arrange
        var publisher = new MqttPublisher(_mockLogger.Object, _mockOptions.Object);

        // Act & Assert - Should not throw
        await publisher.DisposeAsync();
    }

    [Fact]
    public void Constructor_ShouldSetProperties()
    {
        // Act
        var publisher = new MqttPublisher(_mockLogger.Object, _mockOptions.Object);

        // Assert
        Assert.NotNull(publisher);
        Assert.False(publisher.IsConnected); // Should be false before initialization
    }
}

// Test for the IPublisher interface contract compliance
public class IPublisherContractTest
{
    [Fact]
    public void ConsolePublisher_ShouldImplementIPublisher()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ConsolePublisher>>();
        
        // Act
        var publisher = new ConsolePublisher(mockLogger.Object);
        
        // Assert
        Assert.IsAssignableFrom<IPublisher>(publisher);
    }

    [Fact]
    public void MqttPublisher_ShouldImplementIPublisher()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<MqttPublisher>>();
        var mockOptions = new Mock<IOptions<Options>>();
        mockOptions.Setup(x => x.Value).Returns(new Options());
        
        // Act
        var publisher = new MqttPublisher(mockLogger.Object, mockOptions.Object);
        
        // Assert
        Assert.IsAssignableFrom<IPublisher>(publisher);
    }

    [Theory]
    [InlineData("")]
    [InlineData("test-ap")]
    [InlineData("ap-with-long-name")]
    public async Task IPublisher_PublishClientsAsync_ShouldAcceptValidHostnames(string hostname)
    {
        // Arrange
        var mockLogger = new Mock<ILogger<ConsolePublisher>>();
        IPublisher publisher = new ConsolePublisher(mockLogger.Object);
        var connectedClients = new List<string> { "client1" };
        var disconnectedClients = new List<string> { "client2" };

        // Act & Assert - Should not throw
        await publisher.PublishClientsAsync(hostname, connectedClients, disconnectedClients);
    }
}