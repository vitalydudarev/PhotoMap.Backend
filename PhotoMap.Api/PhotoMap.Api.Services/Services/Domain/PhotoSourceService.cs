using System.Text.Json;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Repositories;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Exceptions;

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
    
    public async Task<PhotoSource> GetByIdAsync(long id)
    {
        var photoSource = await _photoSourceRepository.GetByIdAsync(id);
        if (photoSource != null)
        {
            return photoSource;
        }

        throw new NotFoundException($"Photo source entity with ID {id} not found.");
    }
    
    public async Task<ClientAuthSettings?> GetSourceClientAuthSettingsAsync(long id)
    {
        var source = await _photoSourceRepository.GetByIdAsync(id);
        
        return source?.ClientAuthSettings;
    }
}