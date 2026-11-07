using System.Text.Json.Serialization;
using PhotoMap.Shared.Models;

namespace PhotoMap.Shared.Messages;

/// <summary>
/// What the worker made of a <see cref="ProcessImageRequest"/>: the processed image, or why it could not be
/// processed. Sent from the worker to the API, which saves the photo; published to <see cref="Topic"/> when Kafka
/// is enabled.
/// </summary>
/// <remarks>
/// A message of the pipeline, unlike <see cref="Events.PhotoSavedEvent"/>: it moves the work on to its one consumer,
/// rather than telling whoever is interested that something has happened. The photo may yet fail to be saved.
/// </remarks>
public sealed class PhotoProcessed
{
    public const string Topic = "photo-processed";

    /// <summary>
    /// The <see cref="ProcessImageRequest.RequestId"/> of the request this is the result of.
    /// </summary>
    public required Guid RequestId { get; init; }

    public required long UserId { get; init; }
    public required long PhotoSourceId { get; init; }
    public required string FileName { get; init; }

    /// <summary>
    /// Null when the image could not be processed.
    /// </summary>
    public ProcessedImage? Image { get; init; }

    /// <summary>
    /// Why the image could not be processed, null when it has been.
    /// </summary>
    public string? Error { get; init; }

    [JsonIgnore]
    public bool Succeeded => Image != null;

    /// <summary>
    /// The images of one source of a user go to one partition, in the order processed.
    /// </summary>
    public string GetKey() => $"{UserId}:{PhotoSourceId}";

    public static PhotoProcessed Success(ProcessImageRequest request, ProcessedImage image) => new()
    {
        RequestId = request.RequestId,
        UserId = request.UserId,
        PhotoSourceId = request.PhotoSourceId,
        FileName = request.DownloadedFileInfo.ResourceName,
        Image = image
    };

    public static PhotoProcessed Failure(ProcessImageRequest request, string error) => new()
    {
        RequestId = request.RequestId,
        UserId = request.UserId,
        PhotoSourceId = request.PhotoSourceId,
        FileName = request.DownloadedFileInfo.ResourceName,
        Error = error
    };
}
