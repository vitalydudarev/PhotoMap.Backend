using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Domain.Services;

public interface IUserPhotoSourceService
{
    Task<AuthResult?> GetAuthSettings(long userId, long photoSourceId);
    Task UpdateAuthSettings(long userId, long photoSourceId, AuthResult authResult);
}