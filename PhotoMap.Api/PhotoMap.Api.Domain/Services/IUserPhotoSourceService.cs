using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Domain.Services;

public interface IUserPhotoSourceService
{
    Task<IEnumerable<UserPhotoSource>> GetUserPhotoSourcesAsync(long userId);
    Task<UserAuthSettings?> GetUserAuthSettingsAsync(long userId, long photoSourceId);
    Task UpdateUserPhotoSourceAuthResultAsync(long userId, long photoSourceId, UserAuthSettings userAuthSettings);
}