using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// A file of the folder of a photo source to download.
/// </summary>
/// <param name="Name">The name of the file, which is unique in its folder.</param>
/// <param name="Path">What the source downloads the file by.</param>
/// <param name="Size">The size of the file in bytes, as the source tells it.</param>
/// <param name="Content">Whether the file is a photo or a video.</param>
public sealed record SourceFile(string Name, string Path, long Size, SourceDownloadContent Content);

/// <summary>
/// A preview image of a file of a photo source, as the source makes it.
/// </summary>
public sealed record SourcePreview(byte[] Contents, string ContentType);
