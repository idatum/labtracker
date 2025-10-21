namespace LabTracker;

/// <summary>
/// Null implementation of IPublished that returns empty states when initialization is disabled
/// </summary>
public class NullPublishedReader : IPublished
{
    public bool ForceSnapshot => false;

    public Task<Dictionary<string, ClientState>> ReadCurrentStatesAsync()
    {
        // Return empty dictionary when initialization is disabled
        return Task.FromResult(new Dictionary<string, ClientState>());
    }
}