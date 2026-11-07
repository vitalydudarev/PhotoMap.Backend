using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Domain.Services;

/// <summary>
/// The outbox messages, as the relay publishing them sees them. They are added with the change they report, e.g.
/// by <see cref="IPhotoService.AddRangeAsync"/>.
/// </summary>
public interface IOutboxService
{
    /// <summary>
    /// The messages not published yet, oldest first.
    /// </summary>
    Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int maxCount, CancellationToken cancellationToken);

    Task MarkPublishedAsync(IReadOnlyCollection<long> ids, DateTimeOffset publishedOn);

    /// <summary>
    /// Counts a failed attempt to publish the messages, they stay waiting to be published.
    /// </summary>
    Task RecordFailedAsync(IReadOnlyCollection<long> ids, string error);

    /// <returns>How many messages have been deleted.</returns>
    Task<int> DeletePublishedAsync(DateTimeOffset publishedBefore);
}
