using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Exceptions;
using PhotoMap.Api.Services.Services;
using PhotoMap.Shared.Yandex.Disk;

namespace PhotoMap.Api.Services;

/// <summary>
/// Reads the metadata of the files of the videos saved without it: those saved before the metadata was read, and
/// those whose metadata could not be downloaded. Only the parts of a file that hold the metadata are downloaded,
/// all of it when the server does not send parts, see <see cref="YandexDiskVideoSource.ReadExifAsync(string, long, CancellationToken)"/>.
/// </summary>
public class VideoExifBackfiller
{
    private const int BatchSize = 100;
    private const int MaxParallelDownloads = 4;

    private readonly IVideoService _videoService;
    private readonly IVideoProcessingService _videoProcessingService;
    private readonly IUserPhotoSourceService _userPhotoSourceService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<VideoExifBackfiller> _logger;

    public VideoExifBackfiller(
        IVideoService videoService,
        IVideoProcessingService videoProcessingService,
        IUserPhotoSourceService userPhotoSourceService,
        IHttpClientFactory httpClientFactory,
        ILogger<VideoExifBackfiller> logger)
    {
        _videoService = videoService;
        _videoProcessingService = videoProcessingService;
        _userPhotoSourceService = userPhotoSourceService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Reads the metadata of every video saved without it, a batch at a time. A video whose file cannot be
    /// downloaded is left as it is, for the next time; so are the videos of a source the user is not authorized in,
    /// and of a source whose videos are being processed, which reads their metadata itself.
    /// </summary>
    public async Task<VideoExifBackfill> ReadAllAsync(CancellationToken cancellationToken)
    {
        var sources = new Dictionary<(long UserId, long PhotoSourceId), YandexDiskVideoSource?>();
        var result = new VideoExifBackfill(0, 0, 0);
        var afterId = 0L;

        while (true)
        {
            var videos = await _videoService.GetWithoutExifAsync(afterId, BatchSize);
            if (videos.Count == 0)
            {
                return result;
            }

            afterId = videos[^1].Id;

            var exifByVideoId = new ConcurrentDictionary<long, VideoExif>();
            var failedCount = 0;
            var skippedCount = 0;

            foreach (var group in videos.GroupBy(a => (a.UserId, a.PhotoSourceId)))
            {
                if (!sources.TryGetValue(group.Key, out var videoSource))
                {
                    videoSource = await CreateVideoSourceAsync(group.Key.UserId, group.Key.PhotoSourceId);
                    sources[group.Key] = videoSource;
                }

                if (videoSource == null || _videoProcessingService.IsRunning(group.Key.UserId, group.Key.PhotoSourceId))
                {
                    skippedCount += group.Count();
                    continue;
                }

                var authFailed = false;
                var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = MaxParallelDownloads, CancellationToken = cancellationToken };

                await Parallel.ForEachAsync(group, parallelOptions, async (video, token) =>
                {
                    if (Volatile.Read(ref authFailed))
                    {
                        Interlocked.Increment(ref skippedCount);
                        return;
                    }

                    switch (await TryReadExifAsync(videoSource, video, token))
                    {
                        case { } exif:
                            exifByVideoId[video.Id] = exif;
                            break;
                        case null when video.FolderPath == null:
                            Interlocked.Increment(ref skippedCount);
                            break;
                        default:
                            Interlocked.Increment(ref failedCount);
                            break;
                    }
                });

                if (authFailed)
                {
                    _logger.LogWarning(
                        "Photo source {PhotoSourceId} no longer accepts the token of user {UserId}, the metadata of its videos is read once they authorize again",
                        group.Key.PhotoSourceId, group.Key.UserId);

                    sources[group.Key] = null;
                }

                async Task<VideoExif?> TryReadExifAsync(YandexDiskVideoSource source, Video video, CancellationToken token)
                {
                    if (video.FolderPath == null)
                    {
                        // the path of the file is not known
                        return null;
                    }

                    try
                    {
                        return await source.ReadExifAsync(YandexDiskVideoSource.GetPath(video.FolderPath, video.FileName),
                            video.Size, token);
                    }
                    catch (PhotoSourceException e) when (e.IsAuthError)
                    {
                        // the other videos of the source would fail the same way, until the user authorizes again
                        Volatile.Write(ref authFailed, true);

                        return null;
                    }
                    catch (PhotoSourceException e)
                    {
                        _logger.LogWarning(e, "The metadata of video {VideoId} ({FileName}) cannot be downloaded", video.Id,
                            video.FileName);

                        return null;
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        // MetadataExtractor fails the same way on the same file, it is not read again
                        _logger.LogWarning(e, "The metadata of video {VideoId} ({FileName}) cannot be read, it is saved without it",
                            video.Id, video.FileName);

                        return VideoExif.Empty;
                    }
                }
            }

            await _videoService.UpdateExifAsync(exifByVideoId);

            result = new VideoExifBackfill(result.ReadCount + exifByVideoId.Count, result.FailedCount + failedCount,
                result.SkippedCount + skippedCount);

            _logger.LogInformation(
                "Read the metadata of {ReadCount} videos so far, {FailedCount} failed and {SkippedCount} skipped",
                result.ReadCount, result.FailedCount, result.SkippedCount);
        }
    }

    /// <summary>
    /// The source of the videos of the user, null when its videos cannot be downloaded: it is not a Yandex.Disk, or
    /// the user is not authorized in it.
    /// </summary>
    private async Task<YandexDiskVideoSource?> CreateVideoSourceAsync(long userId, long photoSourceId)
    {
        if (!await _videoProcessingService.SupportsVideosAsync(photoSourceId))
        {
            return null;
        }

        var authResult = await _userPhotoSourceService.GetAuthResultAsync(userId, photoSourceId);
        if (authResult == null || !authResult.IsValid)
        {
            _logger.LogInformation(
                "User {UserId} is not authorized in photo source {PhotoSourceId}, the metadata of its videos is read once they are",
                userId, photoSourceId);

            return null;
        }

        var apiClient = new ApiClient(authResult.Token, _httpClientFactory.CreateClient("yandexDiskClient"));

        return new YandexDiskVideoSource(apiClient, _logger);
    }
}

/// <param name="ReadCount">How many videos have had their metadata read and saved.</param>
/// <param name="FailedCount">How many videos could not be downloaded, left for the next time.</param>
/// <param name="SkippedCount">How many videos were left out: of a source not authorized or being processed, or
/// whose path is not known.</param>
public record VideoExifBackfill(int ReadCount, int FailedCount, int SkippedCount);
