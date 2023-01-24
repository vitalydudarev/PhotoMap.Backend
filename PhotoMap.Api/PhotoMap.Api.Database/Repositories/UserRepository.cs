using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Repositories;

namespace PhotoMap.Api.Database.Repositories;

public class UserRepository : IUserRepository
{
    private readonly PhotoMapContext _context;

    public UserRepository(PhotoMapContext context)
    {
        _context = context;
    }
    
    public async Task AddAsync(string name)
    {
        var user = new User();
        user.Name = name;

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();
    }

    public async Task<User?> GetAsync(int id)
    {
        var user = await _context.Users.FindAsync(id);

        return user;
    }

    public async Task UpdateAsync(User user)
    {
        _context.Users.Update(user);

        await _context.SaveChangesAsync();
    }
}
