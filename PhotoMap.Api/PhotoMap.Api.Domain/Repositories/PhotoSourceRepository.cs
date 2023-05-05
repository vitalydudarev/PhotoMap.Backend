using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Domain.Repositories;

public interface IPhotoSourceRepository
{
    Task<IEnumerable<PhotoSource>> GetAsync();
    Task<PhotoSource?> GetByIdAsync(long id);
}