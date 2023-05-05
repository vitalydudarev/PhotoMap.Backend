using Microsoft.EntityFrameworkCore;
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

        return fileSources.Select(a => new PhotoSource { Id = a.Id, Name = a.Name, Settings = a.Settings, ImplementationType = a.ImplementationType });
    }

    public async Task<PhotoSource?> GetByIdAsync(long id)
    {
        var fileSource = await _context.PhotoSources.FindAsync(id);
        if (fileSource != null)
        {
            return new PhotoSource
            {
                Id = fileSource.Id,
                Name = fileSource.Name,
                Settings = fileSource.Settings,
                ImplementationType = fileSource.ImplementationType
            };
        }

        return null;
    }
}