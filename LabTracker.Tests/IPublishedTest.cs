using LabTracker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LabTracker.Tests;

public class ClientStateTest
{
    [Fact]
    public void ClientState_ShouldInitializeWithDefaults()
    {
        // Act
        var clientState = new ClientState();

        // Assert
        Assert.Equal(string.Empty, clientState.ClientId);
        Assert.Null(clientState.ApHostname);
        Assert.False(clientState.IsConnected);
        Assert.Equal(default(DateTime), clientState.LastUpdated);
        Assert.Null(clientState.LastPayload);
    }

    [Fact]
    public void ClientState_ShouldSetProperties()
    {
        // Arrange
        var clientId = "test-client";
        var apHostname = "test-ap";
        var isConnected = true;
        var lastUpdated = DateTime.UtcNow;
        var lastPayload = "online";

        // Act
        var clientState = new ClientState
        {
            ClientId = clientId,
            ApHostname = apHostname,
            IsConnected = isConnected,
            LastUpdated = lastUpdated,
            LastPayload = lastPayload
        };

        // Assert
        Assert.Equal(clientId, clientState.ClientId);
        Assert.Equal(apHostname, clientState.ApHostname);
        Assert.Equal(isConnected, clientState.IsConnected);
        Assert.Equal(lastUpdated, clientState.LastUpdated);
        Assert.Equal(lastPayload, clientState.LastPayload);
    }

    [Theory]
    [InlineData(true, "connected")]
    [InlineData(false, "disconnected")]
    public void ClientState_ShouldSupportDifferentConnectionStates(bool connected, string description)
    {
        // Arrange & Act
        var clientState = new ClientState
        {
            ClientId = $"client-{description}",
            IsConnected = connected
        };

        // Assert
        Assert.Equal(connected, clientState.IsConnected);
        Assert.Equal($"client-{description}", clientState.ClientId);
    }
}

public class MqttPublishedReaderTest
{
    private readonly Mock<ILogger<MqttPublishedReader>> _mockLogger;
    private readonly Mock<IOptions<Options>> _mockOptions;
    private readonly Options _options;

    public MqttPublishedReaderTest()
    {
        _mockLogger = new Mock<ILogger<MqttPublishedReader>>();
        _options = new Options
        {
            Mqtt = new MqttOptions
            {
                BrokerHost = "test-host",
                BrokerPort = 8883,
                TopicPrefix = "labtracker",
                IncludeApInTopic = true,
                ConnectedPayload = "online",
                DisconnectedPayload = "offline",
                UseTls = false
            }
        };
        _mockOptions = new Mock<IOptions<Options>>();
        _mockOptions.Setup(x => x.Value).Returns(_options);
    }

    [Fact]
    public void Constructor_ShouldCreateMqttPublishedReader()
    {
        // Act
        var reader = new MqttPublishedReader(_mockLogger.Object, _mockOptions.Object);

        // Assert
        Assert.NotNull(reader);
    }

    [Fact]
    public void MqttPublishedReader_ShouldImplementIPublished()
    {
        // Act
        var reader = new MqttPublishedReader(_mockLogger.Object, _mockOptions.Object);

        // Assert
        Assert.IsAssignableFrom<IPublished>(reader);
    }

    [Fact]
    public async Task ReadCurrentStatesAsync_WhenConnectionFails_ShouldReturnEmptyDictionary()
    {
        // Arrange - Use invalid host to force connection failure
        var failOptions = new Options
        {
            Mqtt = new MqttOptions
            {
                BrokerHost = "invalid-host-that-does-not-exist-12345",
                BrokerPort = 9999,
                TopicPrefix = "labtracker",
                IncludeApInTopic = true,
                ConnectedPayload = "online"
            }
        };
        
        var mockFailOptions = new Mock<IOptions<Options>>();
        mockFailOptions.Setup(x => x.Value).Returns(failOptions);
        
        var reader = new MqttPublishedReader(_mockLogger.Object, mockFailOptions.Object);

        // Act
        var result = await reader.ReadCurrentStatesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);

        // Verify error was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to read current client states from MQTT")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}

// Test for the IPublished interface contract compliance
public class IPublishedContractTest
{
    [Fact]
    public void MqttPublishedReader_ShouldImplementIPublished()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<MqttPublishedReader>>();
        var mockOptions = new Mock<IOptions<Options>>();
        mockOptions.Setup(x => x.Value).Returns(new Options());
        
        // Act
        var reader = new MqttPublishedReader(mockLogger.Object, mockOptions.Object);
        
        // Assert
        Assert.IsAssignableFrom<IPublished>(reader);
    }

    [Fact]
    public void IPublished_ReadCurrentStatesAsync_ShouldReturnDictionaryOfClientStates()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<MqttPublishedReader>>();
        var mockOptions = new Mock<IOptions<Options>>();
        mockOptions.Setup(x => x.Value).Returns(new Options());
        
        IPublished publisher = new MqttPublishedReader(mockLogger.Object, mockOptions.Object);

        // Act & Assert - Method should exist and be callable
        var method = publisher.GetType().GetMethod("ReadCurrentStatesAsync");
        Assert.NotNull(method);
        Assert.Equal(typeof(Task<Dictionary<string, ClientState>>), method.ReturnType);
    }
}

// Integration-style tests for parsing logic (using reflection to test private methods)
public class MqttPublishedReaderParsingTest
{
    private readonly MqttPublishedReader _reader;
    private readonly Options _options;

    public MqttPublishedReaderParsingTest()
    {
        var mockLogger = new Mock<ILogger<MqttPublishedReader>>();
        _options = new Options
        {
            Mqtt = new MqttOptions
            {
                TopicPrefix = "labtracker",
                IncludeApInTopic = true,
                ConnectedPayload = "online",
                DisconnectedPayload = "offline"
            }
        };
        var mockOptions = new Mock<IOptions<Options>>();
        mockOptions.Setup(x => x.Value).Returns(_options);
        
        _reader = new MqttPublishedReader(mockLogger.Object, mockOptions.Object);
    }

    [Theory]
    [InlineData("labtracker/ap1/client1", "client1", "ap1")]
    [InlineData("labtracker/office-ap/laptop", "laptop", "office-ap")]
    [InlineData("labtracker/wifi-main/AA:BB:CC:DD:EE:FF", "AA:BB:CC:DD:EE:FF", "wifi-main")]
    public void ParseTopic_WithApInTopic_ShouldParseCorrectly(string topic, string expectedClient, string expectedAp)
    {
        // Act
        var parseTopicMethod = typeof(MqttPublishedReader).GetMethod("ParseTopic", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = parseTopicMethod?.Invoke(_reader, new object[] { topic });

        // Assert
        Assert.NotNull(result);
        var (clientId, apHostname) = ((string?, string?))result;
        Assert.Equal(expectedClient, clientId);
        Assert.Equal(expectedAp, apHostname);
    }

    [Theory]
    [InlineData("labtracker/client1", "client1")]
    [InlineData("labtracker/laptop", "laptop")]
    [InlineData("labtracker/AA:BB:CC:DD:EE:FF", "AA:BB:CC:DD:EE:FF")]
    public void ParseTopic_WithoutApInTopic_ShouldParseCorrectly(string topic, string expectedClient)
    {
        // Arrange - Change options to not include AP in topic
        _options.Mqtt.IncludeApInTopic = false;

        // Act
        var parseTopicMethod = typeof(MqttPublishedReader).GetMethod("ParseTopic", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = parseTopicMethod?.Invoke(_reader, new object[] { topic });

        // Assert
        Assert.NotNull(result);
        var (clientId, apHostname) = ((string?, string?))result;
        Assert.Equal(expectedClient, clientId);
        Assert.Null(apHostname); // Should be null when not including AP in topic
    }

    [Theory]
    [InlineData("wrong/ap1/client1")]
    [InlineData("labtracker")]
    [InlineData("labtracker/")]
    [InlineData("")]
    public void ParseTopic_WithInvalidTopic_ShouldReturnNulls(string topic)
    {
        // Act
        var parseTopicMethod = typeof(MqttPublishedReader).GetMethod("ParseTopic", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = parseTopicMethod?.Invoke(_reader, new object[] { topic });

        // Assert
        Assert.NotNull(result);
        var (clientId, apHostname) = ((string?, string?))result;
        Assert.Null(clientId);
        Assert.Null(apHostname);
    }

    [Theory]
    [InlineData("online", true)]
    [InlineData("ONLINE", true)]
    [InlineData("OnLiNe", true)]
    [InlineData("offline", false)]
    [InlineData("OFFLINE", false)]
    [InlineData("OfFlInE", false)]
    [InlineData("disconnected", false)]
    [InlineData("", false)]
    public void IsConnectedPayload_ShouldIdentifyConnectionState(string payload, bool expected)
    {
        // Act
        var isConnectedMethod = typeof(MqttPublishedReader).GetMethod("IsConnectedPayload", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var result = isConnectedMethod?.Invoke(_reader, new object[] { payload });

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expected, (bool)result);
    }

    [Theory]
    [InlineData("client1", "ap1", "ap1:client1")]
    [InlineData("laptop", "office", "office:laptop")]
    [InlineData("AA:BB:CC:DD:EE:FF", "wifi-main", "wifi-main:AA:BB:CC:DD:EE:FF")]
    [InlineData("client1", null, "unknown:client1")]
    [InlineData("client1", Options.AllApsAggregate, Options.AllApsAggregate + ":client1")]
    public void CreateClientStateKey_ShouldFormatCorrectly(string clientId, string? apHostname, string expected)
    {
        // Act
        var createKeyMethod = typeof(MqttPublishedReader).GetMethod("CreateClientStateKey", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = createKeyMethod?.Invoke(null, new object?[] { clientId, apHostname });

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expected, (string)result);
    }
}