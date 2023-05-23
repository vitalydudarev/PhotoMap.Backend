using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Domain.Services;

public interface IUserPhotoSourceService
{
    Task<IEnumerable<UserPhotoSource>> GetUserPhotoSourcesAsync(long userId);
    Task UpdateUserPhotoSourceAuthResultAsync(long userId, long photoSourceId, AuthResult authResult);
}