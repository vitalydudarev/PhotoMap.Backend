using System.Collections.Concurrent;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Shared.Models;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// The files of one page that failed, recorded once all of the page has been processed, along with the state of
/// the run: the database context of the run is not thread safe, while the files of a page download in parallel.
/// </summary>
public sealed class PageFailures
{
    private readonly ConcurrentQueue<FailedFile> _downloadFailures = new();

    public void DownloadFailed(string externalId, string? path, string fileName, string error)
    {
        _downloadFailures.Enqueue(new FailedFile
        {
            ExternalId = externalId,
            Path = path,
            FileName = fileName,
            Stage = FileFailureStage.Download,
            Error = error,
            FailedAt = DateTimeOffset.UtcNow
        });
    }

    /// <param name="processedFiles">The downloaded files of the page, all of them processed.</param>
    public Task RecordAsync(IFailedFileService failedFileService, long userId, long sourceId,
        IReadOnlyCollection<DownloadedFile> processedFiles)
    {
        var failedFiles = _downloadFailures.ToList();
        var savedExternalIds = new List<string>();

        foreach (var file in processedFiles)
        {
            var result = file.Processed.Task.Result;

            if (result.Succeeded)
            {
                savedExternalIds.Add(file.FileInfo.FileId);
            }
            else
            {
                failedFiles.Add(new FailedFile
                {
                    ExternalId = file.FileInfo.FileId,
                    Path = file.FileInfo.Path,
                    FileName = file.FileInfo.ResourceName,
                    Stage = FileFailureStage.Processing,
                    Error = result.Error ?? "Processing failed.",
                    FailedAt = DateTimeOffset.UtcNow
                });
            }
        }

        return failedFileService.RecordAsync(userId, sourceId, failedFiles, savedExternalIds);
    }
}
