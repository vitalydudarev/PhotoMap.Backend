using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Services;
using PhotoMap.Api.Services.Tests.Stubs;
using PhotoMap.Shared.Messaging;

namespace PhotoMap.Api.Services.Tests;

public class OutboxRelayTests
{
    private readonly Mock<IOutboxService> _outboxService = new();
    private readonly MessagePublisherStub _publisher = new();
    private readonly List<long> _publishedIds = [];
    private readonly List<(IReadOnlyCollection<long> Ids, string Error)> _failures = [];

    public OutboxRelayTests()
    {
        _outboxService
            .Setup(a => a.MarkPublishedAsync(It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<DateTimeOffset>()))
            .Callback<IReadOnlyCollection<long>, DateTimeOffset>((ids, _) => _publishedIds.AddRange(ids))
            .Returns(Task.CompletedTask);
        _outboxService
            .Setup(a => a.RecordFailedAsync(It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<string>()))
            .Callback<IReadOnlyCollection<long>, string>((ids, error) => _failures.Add((ids, error)))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task PublishPendingAsync_ShouldPublishTheMessagesInOrder_AndMarkThemPublished()
    {
        // Arrange
        SetUpPending(CreateMessage(1, "a"), CreateMessage(2, "b"), CreateMessage(3, "c"));

        // Act
        var result = await CreateRelay().PublishPendingAsync(10, CancellationToken.None);

        // Assert
        Assert.Equal(new OutboxRelayResult(3, 3, 0), result);
        Assert.Equal(["a", "b", "c"], _publisher.Published.Select(a => a.Payload));
        Assert.All(_publisher.Published, a => Assert.Equal("photo-saved", a.Topic));
        Assert.All(_publisher.Published, a => Assert.Equal("1:2", a.Key));
        Assert.Equal([1, 2, 3], _publishedIds);
        Assert.Empty(_failures);
    }

    [Fact]
    public async Task PublishPendingAsync_ShouldSendTheOutboxIdAsTheMessageId()
    {
        // Arrange
        SetUpPending(CreateMessage(42, "a"));

        // Act
        await CreateRelay().PublishPendingAsync(10, CancellationToken.None);

        // Assert
        var message = Assert.Single(_publisher.Published);
        Assert.Equal("42", message.Headers[MessageHeaders.MessageId]);
    }

    [Fact]
    public async Task PublishPendingAsync_ShouldLeaveTheFailedMessagesPending_AndRecordTheirFailure()
    {
        // Arrange
        SetUpPending(CreateMessage(1, "a"), CreateMessage(2, "fail"), CreateMessage(3, "c"));
        _publisher.FailWhen = payload => payload == "fail";

        // Act
        var result = await CreateRelay().PublishPendingAsync(10, CancellationToken.None);

        // Assert
        Assert.Equal(new OutboxRelayResult(3, 2, 1), result);
        Assert.Equal([1, 3], _publishedIds);
        var failure = Assert.Single(_failures);
        Assert.Equal([2], failure.Ids);
        Assert.Contains("Broker unavailable", failure.Error);
    }

    [Fact]
    public async Task PublishPendingAsync_ShouldPublishNothing_WhenNoMessageIsPending()
    {
        // Arrange
        SetUpPending();

        // Act
        var result = await CreateRelay().PublishPendingAsync(10, CancellationToken.None);

        // Assert
        Assert.Equal(OutboxRelayResult.None, result);
        Assert.Empty(_publisher.Published);
        _outboxService.Verify(a => a.MarkPublishedAsync(It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<DateTimeOffset>()),
            Times.Never);
    }

    private void SetUpPending(params OutboxMessage[] messages)
    {
        _outboxService
            .Setup(a => a.GetPendingAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messages);
    }

    private OutboxRelay CreateRelay()
    {
        return new OutboxRelay(_outboxService.Object, _publisher, NullLogger<OutboxRelay>.Instance);
    }

    private static OutboxMessage CreateMessage(long id, string payload)
    {
        return new OutboxMessage { Id = id, Topic = "photo-saved", Key = "1:2", Payload = payload };
    }
}
