using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PhotoMap.Api.Services.Interfaces;

public interface IPhotoProvider
{
    Task<PhotoFile?> GetPhotoAsync(long id, CancellationToken cancellationToken);
    Task<PhotoStream?> GetPreviewAsync(long id, CancellationToken cancellationToken);
    Task<byte[]?> GetThumbAsync(long id, string size);
}

/// <summary>
/// The contents of a photo, downloaded from the photo source it belongs to.
/// </summary>
public record PhotoFile(byte[] Contents, string FileName, string ContentType);

/// <summary>
/// The contents of a photo or of a preview of it, read as they are downloaded from the photo source. Whoever reads
/// them disposes them.
/// </summary>
public record PhotoStream(Stream Contents, string FileName, string ContentType);
