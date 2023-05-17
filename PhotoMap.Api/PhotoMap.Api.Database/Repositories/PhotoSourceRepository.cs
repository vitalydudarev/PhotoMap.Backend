using Microsoft.EntityFrameworkCore;
using PhotoMap.Api.Database.Entities;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Repositories;

namespace PhotoMap.Api.Database.Repositories;

public class PhotoSourceRepository : IPhotoSourceRepository
{
    private readonly PhotoMapContext _context;

    public PhotoSourceRepository(PhotoMapContext context)
    {
        _context = context;
    }
    
    public async Task<IEnumerable<PhotoSource>> GetAsync()
    {
        var fileSources = await _context.PhotoSources.ToListAsync();

        return fileSources.Select(EntityToModel);
    }

    public async Task<PhotoSource?> GetByIdAsync(long id)
    {
        var fileSource = await _context.PhotoSources.FindAsync(id);
        
        return fileSource != null ? EntityToModel(fileSource) : null;
    }

    private static PhotoSource EntityToModel(PhotoSourceEntity entity)
    {
        return new PhotoSource
        {
            Id = entity.Id,
            Name = entity.Name,
            ServiceSettings = entity.Settings,
            AuthSettings = entity.AuthSettings,
            ServiceImplementationType = entity.ServiceImplementationType,
            SettingsImplementationType = entity.SettingsImplementationType
        };
    }
}