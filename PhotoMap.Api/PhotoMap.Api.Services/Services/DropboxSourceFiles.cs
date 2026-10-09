using Dropbox.Api;
using Dropbox.Api.Files;
using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// The files of the folder of a Dropbox its photos are processed from.
/// </summary>
public sealed class DropboxSourceFiles : ISourceFiles
{
    private const int ListPageSize = 2000;

    /// <summary>
    /// The sizes of the thumbnail, the largest first: a photo Dropbox cannot make one size of may make a smaller one.
    /// </summary>
    private static readonly ThumbnailSize[] PreviewSizes =
    [
        ThumbnailSize.W2048h1536.Instance,
        ThumbnailSize.W1024h768.Instance,
        ThumbnailSize.W960h640.Instance,
        ThumbnailSize.W640h480.Instance,
        ThumbnailSize.W480h320.Instance,
        ThumbnailSize.W256h256.Instance
    ];

    private readonly DropboxClient _dropboxClient;
    private readonly DropboxSettings _settings;

    public DropboxSourceFiles(DropboxClient dropboxClient, DropboxSettings settings)
    {
        _dropboxClient = dropboxClient;
        _settings = settings;
    }

    public int MaxParallelDownloads => Math.Max(_settings.MaxParallelFileDownloads, 1);

    public async Task<IReadOnlyList<SourceFile>> ListAsync(SourceDownloadContent content, string? folder,
        CancellationToken cancellationToken)
    {
        if (folder != null)
        {
            throw new NotSupportedException("The folders of Dropbox cannot be chosen, its files are downloaded from its source folder.");
        }

        var files = new List<SourceFile>();
        var listFolderResult = await _dropboxClient.Files.ListFolderAsync(_settings.SourceFolder, limit: ListPageSize);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var fileMetadata in listFolderResult.Entries.OfType<FileMetadata>())
            {
                var fileContent = DownloadableFormats.GetContent(fileMetadata.Name);
                if (fileContent != null && content.HasFlag(fileContent.Value))
                {
                    // the file ID stays the same when the file is moved or renamed, the path doesn't
                    files.Add(new SourceFile(fileMetadata.Name, fileMetadata.Id, (long)fileMetadata.Size, fileContent.Value));
                }
            }

            if (!listFolderResult.HasMore)
            {
                break;
            }

            listFolderResult = await _dropboxClient.Files.ListFolderContinueAsync(listFolderResult.Cursor);
        }

        return files;
    }

    public async Task CopyToAsync(SourceFile file, Stream destination, CancellationToken cancellationToken)
    {
        using var response = await _dropboxClient.Files.DownloadAsync(file.Path);
        await using var contentStream = await response.GetContentAsStreamAsync();

        await contentStream.CopyToAsync(destination, cancellationToken);
    }

    /// <summary>
    /// The largest thumbnail Dropbox makes of the file. Dropbox makes thumbnails of photos only, of the formats it
    /// supports and up to 20 MB: there is no preview of any other file.
    /// </summary>
    public async Task<SourcePreview?> DownloadPreviewAsync(string reference, CancellationToken cancellationToken)
    {
        foreach (var size in PreviewSizes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var response = await _dropboxClient.Files.GetThumbnailAsync(reference, ThumbnailFormat.Jpeg.Instance, size);
                var contents = await response.GetContentAsByteArrayAsync();

                return contents.Length == 0 ? null : new SourcePreview(contents, "image/jpeg");
            }
            catch (ApiException<ThumbnailError> e) when (e.ErrorResponse.IsConversionError)
            {
                // a smaller thumbnail may be made of it
            }
            catch (ApiException<ThumbnailError> e) when (e.ErrorResponse.IsUnsupportedExtension ||
                                                         e.ErrorResponse.IsUnsupportedImage ||
                                                         e.ErrorResponse.IsEncryptedContent)
            {
                return null;
            }
        }

        return null;
    }

    public bool IsAuthError(Exception exception)
    {
        return exception is AuthException;
    }

    public void Dispose()
    {
        _dropboxClient.Dispose();
    }
}
