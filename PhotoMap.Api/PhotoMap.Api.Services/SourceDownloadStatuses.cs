using System.Collections.Concurrent;
using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Services;

/// <summary>
/// The status of the last download of each kind of each photo source of each user. Kept in memory only: the files downloaded stay
/// on the disk, and a download started again after a restart skips them.
/// </summary>
public sealed class SourceDownloadStatuses
{
    private readonly ConcurrentDictionary<(long UserId, long SourceId, SourceDownloadKind Kind), SourceDownloadStatus> _statuses = new();

    public SourceDownloadStatus? Get(long userId, long sourceId, SourceDownloadKind kind)
    {
        return _statuses.GetValueOrDefault((userId, sourceId, kind));
    }

    public void Set(long userId, long sourceId, SourceDownloadKind kind, SourceDownloadStatus status)
    {
        _statuses[(userId, sourceId, kind)] = status;
    }
}
