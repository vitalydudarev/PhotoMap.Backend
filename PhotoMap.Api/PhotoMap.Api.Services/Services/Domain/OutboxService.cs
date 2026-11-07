using Microsoft.EntityFrameworkCore;
using PhotoMap.Api.Database;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;

namespace PhotoMap.Api.Services.Services.Domain;

/// <remarks>
/// One relay reads the messages. A second instance of the application would read the same ones and publish them
/// twice; the relays would then take the messages with SELECT ... FOR UPDATE SKIP LOCKED, so that a message read by
/// one is skipped by the others.
/// </remarks>
public class OutboxService : IOutboxService
{
    private readonly PhotoMapContext _context;

    public OutboxService(PhotoMapContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int maxCount, CancellationToken cancellationToken)
    {
        return await _context.OutboxMessages
            .Where(a => a.PublishedOn == null)
            .OrderBy(a => a.Id)
            .Take(maxCount)
            .AsNoTracking()
            .Select(a => new OutboxMessage
            {
                Id = a.Id,
                Topic = a.Topic,
                Key = a.Key,
                Payload = a.Payload,
                CreatedOn = a.CreatedOn,
                Attempts = a.Attempts,
                LastError = a.LastError
            })
            .ToListAsync(cancellationToken);
    }

    public async Task MarkPublishedAsync(IReadOnlyCollection<long> ids, DateTimeOffset publishedOn)
    {
        if (ids.Count == 0)
        {
            return;
        }

        await _context.OutboxMessages
            .Where(a => ids.Contains(a.Id))
            .ExecuteUpdateAsync(a => a.SetProperty(b => b.PublishedOn, publishedOn));
    }

    public async Task RecordFailedAsync(IReadOnlyCollection<long> ids, string error)
    {
        if (ids.Count == 0)
        {
            return;
        }

        await _context.OutboxMessages
            .Where(a => ids.Contains(a.Id))
            .ExecuteUpdateAsync(a => a
                .SetProperty(b => b.Attempts, b => b.Attempts + 1)
                .SetProperty(b => b.LastError, error));
    }

    public Task<int> DeletePublishedAsync(DateTimeOffset publishedBefore)
    {
        return _context.OutboxMessages
            .Where(a => a.PublishedOn != null && a.PublishedOn < publishedBefore)
            .ExecuteDeleteAsync();
    }
}
