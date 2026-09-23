using System.Threading;
using System.Threading.Tasks;

namespace PhotoMap.Api.Services.Interfaces;

public interface IPhotoProvider
{
    Task<PhotoFile?> GetPhotoAsync(long id, CancellationToken cancellationToken);
    Task<byte[]?> GetThumbAsync(long id, string size);
}

/// <summary>
/// The contents of a photo, downloaded from the photo source it belongs to.
/// </summary>
public record PhotoFile(byte[] Contents, string FileName, string ContentType);
