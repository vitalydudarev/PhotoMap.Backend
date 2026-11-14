using Microsoft.EntityFrameworkCore;
using PhotoMap.Api.Database.Entities;
using PhotoMap.Api.Domain.Repositories;

namespace PhotoMap.Api.Database.Repositories;

public class MaintenanceTaskRepository : IMaintenanceTaskRepository
{
    private readonly PhotoMapContext _context;

    public MaintenanceTaskRepository(PhotoMapContext context)
    {
        _context = context;
    }

    public Task<bool> IsCompletedAsync(string name)
    {
        return _context.CompletedMaintenanceTasks.AnyAsync(a => a.Name == name);
    }

    public async Task MarkCompletedAsync(string name)
    {
        if (await IsCompletedAsync(name))
        {
            return;
        }

        _context.CompletedMaintenanceTasks.Add(new CompletedMaintenanceTaskEntity
        {
            Name = name,
            CompletedOn = DateTimeOffset.UtcNow
        });

        await _context.SaveChangesAsync();
    }
}
