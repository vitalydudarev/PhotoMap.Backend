using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Domain.Services;

public interface IUserPhotoSourceService
{
    Task<IEnumerable<UserPhotoSource>> GetUserPhotoSourcesAsync(long userId);
    Task<UserAuthResult?> GetAuthResultAsync(long userId, long photoSourceId);
    Task UpdateAuthResultAsync(long userId, long photoSourceId, UserAuthResult userAuthResult);
}