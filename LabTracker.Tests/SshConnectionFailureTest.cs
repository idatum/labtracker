using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LabTracker.Tests;

public class SshConnectionFailureTest
{
    private readonly Mock<ILogger<Worker>> _mockLogger;
    private readonly Mock<IHostApplicationLifetime> _mockHostLifetime;
    private readonly Mock<IOptions<Options>> _mockOptions;
    private readonly Mock<IPublisher> _mockPublisher;
    private readonly Mock<IPublished> _mockPublished;
    private readonly Mock<IClientInfoProvider> _mockClientProvider;
    private readonly Options _testOptions;

    public SshConnectionFailureTest()
    {
        _mockLogger = new Mock<ILogger<Worker>>();
        _mockHostLifetime = new Mock<IHostApplicationLifetime>();
        _mockOptions = new Mock<IOptions<Options>>();
        _mockPublisher = new Mock<IPublisher>();
        _mockPublished = new Mock<IPublished>();
        _mockClientProvider = new Mock<IClientInfoProvider>();

        _testOptions = new Options
        {
            Unifi = new UnifiOptions
            {
                AccessPoints = new[] { "192.168.1.1", "192.168.1.2" }
            },
            DelayMs = 1000
        };
        _mockOptions.Setup(x => x.Value).Returns(_testOptions);

        // Setup publisher initialization
        _mockPublisher.Setup(x => x.InitializeAsync()).Returns(Task.CompletedTask);
        _mockPublished.Setup(x => x.ReadCurrentStatesAsync())
            .ReturnsAsync(new Dictionary<string, ClientState>());
    }

    [Fact]
    public async Task Worker_WhenSshConnectionFails_ShouldTriggerServiceRestart()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        
        // Setup SSH failure for one host
        _mockClientProvider.Setup(x => x.GetClientsAsync("192.168.1.1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SshConnectionException("Connection failed"));
        
        _mockClientProvider.Setup(x => x.GetClientsAsync("192.168.1.2", It.IsAny<CancellationToken>()))
            .ReturnsAsync((null, new List<ClientInfo>()));

        var worker = new Worker(_mockLogger.Object, _mockHostLifetime.Object, _mockOptions.Object, 
            _mockPublisher.Object, _mockPublished.Object, _mockClientProvider.Object);

        // Act & Assert
        // The worker should catch SSH exceptions and call StopApplication
        var executeTask = worker.StartAsync(cts.Token);
        
        // Give it a moment to start and process
        await Task.Delay(100);
        
        // Cancel to stop the worker
        cts.Cancel();
        
        try
        {
            await executeTask;
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation occurs
        }

        // Verify that StopApplication was called due to SSH failure
        _mockHostLifetime.Verify(x => x.StopApplication(), Times.AtLeastOnce);
    }

    [Fact]
    public void SshConnectionException_ShouldBeCreatedCorrectly()
    {
        // Test the custom exception
        var message = "Test SSH failure";
        var innerException = new Exception("Inner exception");
        
        var exception1 = new SshConnectionException(message);
        var exception2 = new SshConnectionException(message, innerException);
        
        Assert.Equal(message, exception1.Message);
        Assert.Equal(message, exception2.Message);
        Assert.Equal(innerException, exception2.InnerException);
    }

    [Fact]
    public async Task SshClientProvider_WhenConnectionFails_ShouldThrowSshConnectionException()
    {
        // This test verifies that SSH connection failures are properly wrapped
        var mockLogger = new Mock<ILogger<SshClientProvider>>();
        var mockOptions = new Mock<IOptions<Options>>();
        var testOptions = new Options
        {
            Unifi = new UnifiOptions
            {
                Username = "test",
                PrivateKeyPath = "/nonexistent/key"
            },
            ConnectionTimeoutSeconds = 1,
            CommandTimeoutSeconds = 1
        };
        mockOptions.Setup(x => x.Value).Returns(testOptions);

        var provider = new SshClientProvider(mockLogger.Object, mockOptions.Object);
        
        // This will fail because the host/key doesn't exist
        var exception = await Assert.ThrowsAsync<SshConnectionException>(
            () => provider.GetClientsAsync("invalid-host", CancellationToken.None));
        
        Assert.Contains("Failed to connect to SSH host invalid-host", exception.Message);
        Assert.NotNull(exception.InnerException);
    }
}