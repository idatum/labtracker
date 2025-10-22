using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using LabTracker;
using LabTracker.Mqtt;
using LabTracker.Unifi;

namespace LabTracker.Tests;

public class CombinedPublishedReaderTest
{
    private readonly Mock<ILogger<CombinedPublishedReader>> _mockLogger;
    private readonly Mock<IOptions<Options>> _mockOptions;
    private readonly Mock<IUniFiApiClient> _mockUnifiApiClient;
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly Options _testOptions;

    public CombinedPublishedReaderTest()
    {
        _mockLogger = new Mock<ILogger<CombinedPublishedReader>>();
        _mockOptions = new Mock<IOptions<Options>>();
        _mockUnifiApiClient = new Mock<IUniFiApiClient>();
        _mockLoggerFactory = new Mock<ILoggerFactory>();

        _testOptions = new Options
        {
            Mqtt = new MqttOptions
            {
                BrokerHost = "test-broker",
                BrokerPort = 1883,
                TopicPrefix = "test"
            },
            UnifiApi = new UnifiApiOptions
            {
                BaseUrl = "unifi.example.com",
                Key = "test-key"
            }
        };

        _mockOptions.Setup(x => x.Value).Returns(_testOptions);
        
        // Setup logger factory to return mock loggers using the non-generic CreateLogger method
        _mockLoggerFactory.Setup(x => x.CreateLogger(typeof(MqttPublishedReader).FullName!))
            .Returns(new Mock<ILogger<MqttPublishedReader>>().Object);
        _mockLoggerFactory.Setup(x => x.CreateLogger(typeof(UnifiPublishedReader).FullName!))
            .Returns(new Mock<ILogger<UnifiPublishedReader>>().Object);
    }

    [Fact]
    public void Constructor_ShouldCreateCombinedPublishedReader()
    {
        // Act
        var reader = new CombinedPublishedReader(_mockLogger.Object, _mockOptions.Object, 
            _mockUnifiApiClient.Object, _mockLoggerFactory.Object);

        // Assert
        Assert.NotNull(reader);
        Assert.True(reader.ForceSnapshot);
    }

    [Fact]
    public void ForceSnapshot_ShouldReturnTrue()
    {
        // Arrange
        var reader = new CombinedPublishedReader(_mockLogger.Object, _mockOptions.Object, 
            _mockUnifiApiClient.Object, _mockLoggerFactory.Object);

        // Act & Assert
        Assert.True(reader.ForceSnapshot);
    }

    [Fact]
    public void CombinedPublishedReader_ShouldImplementIPublished()
    {
        // Arrange
        var reader = new CombinedPublishedReader(_mockLogger.Object, _mockOptions.Object, 
            _mockUnifiApiClient.Object, _mockLoggerFactory.Object);

        // Act & Assert
        Assert.IsAssignableFrom<IPublished>(reader);
    }
}

public class CombinedPublishedReaderContractTest
{
    [Fact]
    public void CombinedPublishedReader_ShouldImplementIPublished()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<CombinedPublishedReader>>();
        var mockOptions = new Mock<IOptions<Options>>();
        var mockUnifiApiClient = new Mock<IUniFiApiClient>();
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        
        mockOptions.Setup(x => x.Value).Returns(new Options());
        mockLoggerFactory.Setup(x => x.CreateLogger(typeof(MqttPublishedReader).FullName!))
            .Returns(new Mock<ILogger<MqttPublishedReader>>().Object);
        mockLoggerFactory.Setup(x => x.CreateLogger(typeof(UnifiPublishedReader).FullName!))
            .Returns(new Mock<ILogger<UnifiPublishedReader>>().Object);

        // Act
        var reader = new CombinedPublishedReader(mockLogger.Object, mockOptions.Object, 
            mockUnifiApiClient.Object, mockLoggerFactory.Object);

        // Assert
        Assert.IsAssignableFrom<IPublished>(reader);
    }
}