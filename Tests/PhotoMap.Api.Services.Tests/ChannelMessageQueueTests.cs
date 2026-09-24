using PhotoMap.Shared.Queues;

namespace PhotoMap.Api.Services.Tests;

public class ChannelMessageQueueTests
{
    private static readonly TimeSpan MaxWait = TimeSpan.FromMilliseconds(100);

    [Fact]
    public async Task ReadBatchAsync_ShouldReadUpToMaxCount()
    {
        // Arrange
        var queue = new ChannelMessageQueue<int>();
        for (var i = 0; i < 5; i++)
        {
            await queue.EnqueueAsync(i);
        }

        // Act
        var first = await queue.ReadBatchAsync(3, MaxWait);
        var second = await queue.ReadBatchAsync(3, MaxWait);

        // Assert
        Assert.Equal([0, 1, 2], first);
        Assert.Equal([3, 4], second);
    }

    [Fact]
    public async Task ReadBatchAsync_ShouldTakeTheMessagesArrivingWhileItWaits()
    {
        // Arrange
        var queue = new ChannelMessageQueue<int>();
        await queue.EnqueueAsync(0);

        // Act
        var batchTask = queue.ReadBatchAsync(2, TimeSpan.FromSeconds(10)).AsTask();
        await queue.EnqueueAsync(1);
        var batch = await batchTask;

        // Assert: full before the time to wait was up
        Assert.Equal([0, 1], batch);
    }

    [Fact]
    public async Task ReadBatchAsync_ShouldWaitForTheFirstMessage()
    {
        // Arrange
        var queue = new ChannelMessageQueue<int>();

        // Act
        var batchTask = queue.ReadBatchAsync(3, MaxWait).AsTask();
        await Task.Delay(MaxWait * 2);
        var completedEmpty = batchTask.IsCompleted;

        await queue.EnqueueAsync(7);
        var batch = await batchTask;

        // Assert: the time to wait for more starts with the first message
        Assert.False(completedEmpty);
        Assert.Equal([7], batch);
    }
}
