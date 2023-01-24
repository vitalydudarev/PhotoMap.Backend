using Microsoft.EntityFrameworkCore;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Repositories;
using Photo = PhotoMap.Api.Domain.Models.Photo;

namespace PhotoMap.Api.Database.Repositories;

public class PhotoRepository : IPhotoRepository
{
    private readonly PhotoMapContext _context;

    public PhotoRepository(PhotoMapContext context)
    {
        _context = context;
    }
    
    public async Task AddAsync(Photo photo)
    {
        await _context.Photos.AddAsync(photo);
        await _context.SaveChangesAsync();
    }
    
    public async Task<Photo> GetAsync(int id)
    {
        var photo = await _context.Photos.FindAsync(id);
        
        return photo;
    }

    public async Task<Photo> GetByFileNameAsync(string fileName)
    {
        var photo = await _context.Photos.FirstOrDefaultAsync(a => a.FileName == fileName);
        
        return photo;
    }

    public async Task<ComplexResponse<Photo>> GetByUserIdAsync(int userId, int top, int skip)
    {
        var photos = await _context.Photos
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.DateTimeTaken)
            .Skip(skip)
            .Take(top)
            .ToListAsync();

        var totalRecords = await _context.Photos.CountAsync(a => a.UserId == userId);

        return new ComplexResponse<Photo> { Values = photos, TotalCount = totalRecords };
    }

    public async Task DeleteByUserIdAsync(int userId)
    {
        var entities = await _context.Photos.Where(a => a.UserId == userId).ToListAsync();
        _context.Photos.RemoveRange(entities);
        
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAllAsync()
    {
        var entities = await _context.Photos.ToListAsync();
        _context.Photos.RemoveRange(entities);

        await _context.SaveChangesAsync();
    }
}
