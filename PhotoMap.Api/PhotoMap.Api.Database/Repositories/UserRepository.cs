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
    
    public async Task<User?> GetAsync(long id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user != null)
        {
            return new User { Id = user.Id, Name = user.Name };
        }

        return null;
    }
}
