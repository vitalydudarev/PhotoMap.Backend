using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace PhotoMap.Api.Services.Interfaces;

public interface IVideoProvider
{
    /// <summary>
    /// Starts downloading the video, or the range of it asked for, from the photo source it belongs to.
    /// </summary>
    /// <param name="range">The bytes to download, the whole video when null.</param>
    /// <returns>Null when the video is not found.</returns>
    Task<VideoStream?> OpenVideoAsync(long id, RangeHeaderValue? range, CancellationToken cancellationToken);
}

/// <summary>
/// A video being downloaded from its photo source: the response of the source, which the caller reads the contents
/// of as they come and disposes, and the media type of the video as the source reported it when the video was
/// listed.
/// </summary>
public sealed record VideoStream(HttpResponseMessage Response, string? MimeType);
