using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Domain.Services;

public interface IPhotoSourceService
{
    Task<PhotoSource> GetByIdAsync(long id);
    Task<ClientAuthSettings?> GetSourceClientAuthSettingsAsync(long id);
}