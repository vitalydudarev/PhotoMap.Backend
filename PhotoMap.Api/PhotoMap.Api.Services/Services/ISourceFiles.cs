using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// The files of the folder a photo source has its photos processed from, as a download takes them.
/// </summary>
public interface ISourceFiles : IDisposable
{
    /// <summary>
    /// How many files are downloaded at a time.
    /// </summary>
    int MaxParallelDownloads { get; }

    /// <summary>
    /// Lists the files of the folder, the photos, the videos or both.
    /// </summary>
    /// <param name="folder">The folder to list, the one the photos are processed from when null.</param>
    /// <param name="includeSubfolders">Whether the files of the folders within the folder, at any depth, are listed
    /// as well.</param>
    /// <exception cref="NotSupportedException">A folder is given to a source whose folders cannot be chosen.</exception>
    Task<IReadOnlyList<SourceFile>> ListAsync(SourceDownloadContent content, string? folder, bool includeSubfolders,
        CancellationToken cancellationToken);

    /// <summary>
    /// Downloads the file into the stream, as its contents come.
    /// </summary>
    Task CopyToAsync(SourceFile file, Stream destination, CancellationToken cancellationToken);

    /// <summary>
    /// Downloads the largest preview the source makes of the file.
    /// </summary>
    /// <param name="reference">What the source finds the file by: its path, or the Dropbox file ID.</param>
    /// <returns>null when the source has no preview of the file.</returns>
    Task<SourcePreview?> DownloadPreviewAsync(string reference, CancellationToken cancellationToken);

    /// <returns>Whether the source has failed with the access token it no longer accepts, which fails every other
    /// file as well.</returns>
    bool IsAuthError(Exception exception);
}
