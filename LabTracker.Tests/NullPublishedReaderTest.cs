using Xunit;

namespace LabTracker.Tests;

public class NullPublishedReaderTest
{
    [Fact]
    public async Task ReadCurrentStatesAsync_ReturnsEmptyDictionary()
    {
        // Arrange
        var reader = new NullPublishedReader();

        // Act
        var result = await reader.ReadCurrentStatesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
        Assert.IsType<Dictionary<string, ClientState>>(result);
    }

    [Fact]
    public void NullPublishedReader_ShouldImplementIPublished()
    {
        // Arrange & Act
        var reader = new NullPublishedReader();

        // Assert
        Assert.IsAssignableFrom<IPublished>(reader);
    }
}