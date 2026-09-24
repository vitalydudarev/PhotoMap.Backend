namespace PhotoMap.Api.Services.Services;

public interface IImageStore
{
    /// <param name="externalId">The ID the photo source assigned to the file, which tells apart files of the same
    /// name.</param>
    Task<string> SaveThumbnailAsync(byte[] bytes, string fileName, string? externalId, string userName, string source,
        int size);
}
