using Microsoft.EntityFrameworkCore;
using PhotoMap.Api.Domain.Repositories;

namespace PhotoMap.Api.Database.Repositories;

public class FileRepository : IFileRepository
{
    private readonly PhotoMapContext _context;

    public FileRepository(PhotoMapContext context)
    {
        _context = context;
    }

    public async Task<Domain.Models.File> AddAsync(Domain.Models.File incomingFile)
    {
        var entityEntry = await _context.Files.AddAsync(incomingFile);

        await _context.SaveChangesAsync();

        return entityEntry.Entity;
    }

    public async Task<Domain.Models.File> GetAsync(long fileId)
    {
        var dbEntry = await _context.Files.FindAsync(fileId);
        
        return dbEntry;
    }

    public async Task<Domain.Models.File> GetByFileNameAsync(string fileName)
    {
        var dbEntry = await _context.Files.FirstOrDefaultAsync(a => a.FileName == fileName);
        
        return dbEntry;
    }

    public async Task<IEnumerable<Domain.Models.File>> GetAllAsync()
    {
        var dbEntries = await _context.Files.ToListAsync();
        
        return dbEntries;
    }

    public async Task DeleteAsync(long fileId)
    {
        var file = await _context.Files.FindAsync(fileId);
        if (file != null)
            _context.Files.Remove(file);

        await _context.SaveChangesAsync();
    }

    public async Task DeleteAllAsync()
    {
        var file = await _context.Files.ToListAsync();
        _context.Files.RemoveRange(file);

        await _context.SaveChangesAsync();
    }
}
