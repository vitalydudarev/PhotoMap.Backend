using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services.Tests;

public class PhotoCategorizationSignalTests
{
    private readonly PhotoCategorizationSignal _signal = new();

    [Fact]
    public async Task WaitAsync_ShouldWait_UntilNotified()
    {
        // Act
        var wait = _signal.WaitAsync(CancellationToken.None);

        // Assert
        Assert.False(wait.IsCompleted);

        _signal.Notify();
        await wait.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Notify_ShouldComeDownToOneWait_WhenGivenSeveralTimes()
    {
        // Arrange
        _signal.Notify();
        _signal.Notify();

        // Act
        await _signal.WaitAsync(CancellationToken.None);
        var second = _signal.WaitAsync(CancellationToken.None);

        // Assert
        Assert.False(second.IsCompleted);
    }
}
