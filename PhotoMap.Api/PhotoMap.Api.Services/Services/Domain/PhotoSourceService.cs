using System.Text.Json;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Repositories;
using PhotoMap.Api.Domain.Services;

namespace PhotoMap.Api.Services.Services.Domain;

public class PhotoSourceService : IPhotoSourceService
{
    private readonly IPhotoSourceRepository _photoSourceRepository;

    public PhotoSourceService(IPhotoSourceRepository photoSourceRepository)
    {
        _photoSourceRepository = photoSourceRepository;
    }

    public Task<IEnumerable<PhotoSource>> GetAsync()
    {
        return _photoSourceRepository.GetAsync();
    }
    
    public Task<PhotoSource?> GetByIdAsync(long id)
    {
        return _photoSourceRepository.GetByIdAsync(id);
    }
    
    public async Task<AuthSettings?> GetSourceAuthSettingsAsync(long id)
    {
        var source = await _photoSourceRepository.GetByIdAsync(id);
        
        return source?.AuthSettings;
    }
}