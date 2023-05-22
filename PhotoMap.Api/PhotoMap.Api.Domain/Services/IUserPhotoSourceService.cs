using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Domain.Services;

public interface IUserPhotoSourceService
{
    Task<IEnumerable<UserPhotoSourceSettings>> GetUserPhotoSourceSettings(long userId);
    Task UpdateAuthSettings(long userId, long photoSourceId, AuthResult authResult);
}