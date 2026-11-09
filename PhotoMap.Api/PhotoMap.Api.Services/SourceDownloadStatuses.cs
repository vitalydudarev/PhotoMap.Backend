using System.Collections.Concurrent;
using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Services;

/// <summary>
/// The status of the last download of each kind of each photo source of each user, with the files it has found
/// missing and what has become of each. Kept in memory only: the files downloaded stay on the disk, and a download
/// started again after a restart skips them.
/// </summary>
public sealed class SourceDownloadStatuses
{
    private readonly ConcurrentDictionary<(long UserId, long SourceId, SourceDownloadKind Kind), SourceDownloadStatus> _statuses = new();

    private readonly ConcurrentDictionary<(long UserId, long SourceId, SourceDownloadKind Kind), MissingDownloads> _missingDownloads = new();

    public SourceDownloadStatus? Get(long userId, long sourceId, SourceDownloadKind kind)
    {
        return _statuses.GetValueOrDefault((userId, sourceId, kind));
    }

    public void Set(long userId, long sourceId, SourceDownloadKind kind, SourceDownloadStatus status)
    {
        _statuses[(userId, sourceId, kind)] = status;
    }

    /// <returns>The files the last download has found missing, in the order it has listed them, empty when there has
    /// been no download since the application started.</returns>
    public IReadOnlyList<MissingDownload> GetMissing(long userId, long sourceId, SourceDownloadKind kind)
    {
        return _missingDownloads.TryGetValue((userId, sourceId, kind), out var missingDownloads) ? missingDownloads.ToList() : [];
    }

    /// <summary>
    /// Replaces the files found missing by the last download with those of a download that has just listed them.
    /// </summary>
    public void SetMissing(long userId, long sourceId, SourceDownloadKind kind, IEnumerable<MissingDownload> missingDownloads)
    {
        _missingDownloads[(userId, sourceId, kind)] = new MissingDownloads(missingDownloads);
    }

    /// <summary>
    /// Records what has become of a file found missing.
    /// </summary>
    /// <param name="error">What the file has failed with, null when it has been downloaded.</param>
    public void RecordMissing(long userId, long sourceId, SourceDownloadKind kind, string folder, string fileName, string? error)
    {
        if (_missingDownloads.TryGetValue((userId, sourceId, kind), out var missingDownloads))
        {
            missingDownloads.Record(folder, fileName, error);
        }
    }

    /// <summary>
    /// The files found missing by a download, whose workers record the outcomes of them at the same time.
    /// </summary>
    private sealed class MissingDownloads
    {
        private readonly Lock _lock = new();
        private readonly List<MissingDownload> _files;
        private readonly Dictionary<(string Folder, string FileName), int> _indexes = [];

        public MissingDownloads(IEnumerable<MissingDownload> files)
        {
            _files = files.ToList();

            for (var i = 0; i < _files.Count; i++)
            {
                _indexes[(_files[i].Folder, _files[i].FileName)] = i;
            }
        }

        public List<MissingDownload> ToList()
        {
            lock (_lock)
            {
                return [.._files];
            }
        }

        public void Record(string folder, string fileName, string? error)
        {
            lock (_lock)
            {
                if (_indexes.TryGetValue((folder, fileName), out var index))
                {
                    _files[index] = _files[index] with
                    {
                        Status = error == null ? MissingDownloadStatus.Downloaded : MissingDownloadStatus.Failed,
                        Error = error
                    };
                }
            }
        }
    }
}
