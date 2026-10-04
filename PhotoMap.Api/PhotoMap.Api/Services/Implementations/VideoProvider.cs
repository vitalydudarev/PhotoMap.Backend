using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Exceptions;
using PhotoMap.Api.Services.Interfaces;
using PhotoMap.Api.Services.Services;
using PhotoMap.Shared.Yandex.Disk;

namespace PhotoMap.Api.Services.Implementations;

public class VideoProvider : IVideoProvider
{
    private readonly IVideoService _videoService;
    private readonly IUserPhotoSourceService _userPhotoSourceService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<VideoProvider> _logger;

    public VideoProvider(
        IVideoService videoService,
        IUserPhotoSourceService userPhotoSourceService,
        IHttpClientFactory httpClientFactory,
        ILogger<VideoProvider> logger)
    {
        _videoService = videoService;
        _userPhotoSourceService = userPhotoSourceService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Only the preview of a video is stored by the application, the video itself is downloaded from Yandex.Disk,
    /// the only source videos are imported from, by its path, with the credentials of the user it belongs to, as
    /// photos are. It is passed on as it comes rather than read whole first: a video can be large, and is played
    /// a range at a time.
    /// </summary>
    public async Task<VideoStream?> OpenVideoAsync(long id, RangeHeaderValue? range, CancellationToken cancellationToken)
    {
        var video = await _videoService.GetAsync(id);
        if (video == null)
        {
            return null;
        }

        if (string.IsNullOrEmpty(video.FolderPath))
        {
            throw new NotFoundException($"Video with ID {id} has no reference to the file in its photo source.");
        }

        var authResult = await _userPhotoSourceService.GetAuthResultAsync(video.UserId, video.PhotoSourceId);
        if (authResult == null || !authResult.IsValid)
        {
            throw new NotAuthorizedException($"User is not authorized in the photo source of video {id}.");
        }

        // Yandex.Disk tokens are not refreshed, the user authorizes again when the token has expired
        var apiClient = new ApiClient(authResult.Token, _httpClientFactory.CreateClient("yandexDiskClient"));
        var videoSource = new YandexDiskVideoSource(apiClient, _logger);

        var response = await videoSource.OpenVideoAsync(YandexDiskVideoSource.GetPath(video.FolderPath, video.FileName),
            range, cancellationToken);

        return new VideoStream(response, video.MimeType);
    }
}
