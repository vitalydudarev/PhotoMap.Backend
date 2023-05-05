using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Domain.Services;

public interface IPhotoSourceService
{
    Task<IEnumerable<PhotoSource>> GetAsync();
    Task<PhotoSource?> GetByIdAsync(long id);
}