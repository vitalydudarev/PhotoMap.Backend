using AutoMapper;
using Microsoft.EntityFrameworkCore;
using PhotoMap.Api.Domain.Repositories;

namespace PhotoMap.Api.Database.Repositories;

public class FileRepository : IFileRepository
{
    private readonly PhotoMapContext _context;
    private readonly IMapper _mapper;

    public FileRepository(PhotoMapContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<Domain.Models.File> AddAsync(Domain.Models.File incomingFile)
    {
        var dbEntry = _mapper.Map<Entities.File>(incomingFile);
        var entityEntry = await _context.Files.AddAsync(dbEntry);

        await _context.SaveChangesAsync();

        return _mapper.Map<Domain.Models.File>(entityEntry.Entity);
    }

    public async Task<Domain.Models.File> GetAsync(long fileId)
    {
        var dbEntry = await _context.Files.FindAsync(fileId);
        
        return _mapper.Map<Domain.Models.File>(dbEntry);
    }

    public async Task<Domain.Models.File> GetByFileNameAsync(string fileName)
    {
        var dbEntry = await _context.Files.FirstOrDefaultAsync(a => a.FileName == fileName);
        
        return _mapper.Map<Domain.Models.File>(dbEntry);
    }

    public async Task<IEnumerable<Domain.Models.File>> GetAllAsync()
    {
        var dbEntries = await _context.Files.ToListAsync();
        
        return _mapper.Map<List<Domain.Models.File>>(dbEntries);
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
