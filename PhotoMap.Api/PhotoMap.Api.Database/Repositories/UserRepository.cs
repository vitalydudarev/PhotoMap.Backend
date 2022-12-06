using AutoMapper;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Repositories;

namespace PhotoMap.Api.Database.Repositories;

public class UserRepository : IUserRepository
{
    private readonly PhotoMapContext _context;
    private readonly IMapper _mapper;

    public UserRepository(PhotoMapContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }
    
    public async Task AddAsync(string name)
    {
        var user = new Entities.User();
        user.Name = name;

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();
    }

    public async Task<User?> GetAsync(int id)
    {
        var dbUser = await _context.Users.FindAsync(id);

        return _mapper.Map<User>(dbUser);
    }

    public async Task UpdateAsync(User user)
    {
        var dbUser = _mapper.Map<Entities.User>(user);

        _context.Users.Update(dbUser);

        await _context.SaveChangesAsync();
    }
}
