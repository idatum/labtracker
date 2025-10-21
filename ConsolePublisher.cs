namespace LabTracker;

/// <summary>
/// Console implementation of the IPublisher interface for testing/debugging.
/// </summary>
public class ConsolePublisher : IPublisher
{
    private readonly ILogger<ConsolePublisher> _logger;

    public ConsolePublisher(ILogger<ConsolePublisher> logger)
    {
        _logger = logger;
    }

    public bool IsConnected => true; // Console is always "connected"

    public Task InitializeAsync()
    {
        _logger.LogInformation("Console publisher initialized");
        return Task.CompletedTask;
    }

    public Task PublishClientsAsync(string apHostname, List<string> connectedClients, List<string> disconnectedClients)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        
        if (connectedClients.Count > 0)
        {
            Console.WriteLine($"[{timestamp}] CONNECTED to {apHostname} ({connectedClients.Count}): {string.Join(", ", connectedClients)}");
        }
        
        if (disconnectedClients.Count > 0)
        {
            Console.WriteLine($"[{timestamp}] DISCONNECTED from {apHostname} ({disconnectedClients.Count}): {string.Join(", ", disconnectedClients)}");
        }
        
        _logger.LogDebug("Published client events for AP {ap} to console", apHostname);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _logger.LogInformation("Console publisher disposed");
        return ValueTask.CompletedTask;
    }
}