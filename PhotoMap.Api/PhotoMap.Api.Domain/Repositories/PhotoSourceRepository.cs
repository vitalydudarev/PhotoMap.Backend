using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Domain.Repositories;

public interface IPhotoSourceRepository
{
    Task<PhotoSource?> GetByIdAsync(long id);
}