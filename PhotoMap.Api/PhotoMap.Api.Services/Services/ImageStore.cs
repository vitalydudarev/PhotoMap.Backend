using System.Security.Cryptography;
using System.Text;
using PhotoMap.Api.Domain.Services;

namespace PhotoMap.Api.Services.Services;

public class ImageStore : IImageStore
{
    private readonly IFileStorage _fileStorage;

    public ImageStore(IFileStorage fileStorage)
    {
        _fileStorage = fileStorage;
    }

    /// <summary>
    /// Files of the same name, e.g. IMG_0001.JPG once the camera numbering has wrapped around, get thumbnails of
    /// their own: the name carries a hash of the ID of the file, which stays the same for the file, so processing
    /// it again overwrites its thumbnails. A file without an ID gets a random name instead. Thumbnails are JPEG
    /// whatever the format of the photo.
    /// </summary>
    public async Task<string> SaveThumbnailAsync(byte[] bytes, string fileName, string? externalId, string userName,
        string source, int size)
    {
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var fileKey = externalId != null
            ? Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(externalId)))[..16]
            : Guid.NewGuid().ToString("N");
        var thumbFileName = $"{fileNameWithoutExtension}_{fileKey}_{size}.jpg";
        var path = Path.Combine(source, userName, "thumbs", thumbFileName);

        await _fileStorage.SaveAsync(path, bytes);

        return path;
    }
}
