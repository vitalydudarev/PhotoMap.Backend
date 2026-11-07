using System.Collections.Concurrent;
using PhotoMap.Shared.Messaging;

namespace PhotoMap.Api.Services.Tests.Stubs;

/// <summary>
/// Records the messages published, in the order published; the ones whose payload <see cref="FailWhen"/> matches
/// fail to be delivered.
/// </summary>
public class MessagePublisherStub : IMessagePublisher
{
    private readonly ConcurrentQueue<PublishedMessage> _published = new();

    public Func<string, bool> FailWhen { get; set; } = _ => false;

    public IReadOnlyList<PublishedMessage> Published => _published.ToList();

    public async Task PublishAsync(string topic, string key, string payload, IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken)
    {
        // as the broker, acknowledges later
        await Task.Yield();

        if (FailWhen(payload))
        {
            throw new MessagePublishException("Broker unavailable");
        }

        _published.Enqueue(new PublishedMessage(topic, key, payload, headers));
    }

    public sealed record PublishedMessage(string Topic, string Key, string Payload, IReadOnlyDictionary<string, string> Headers);
}
