using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Domain.Repositories;

public interface IUserRepository
{
    Task AddAsync(string name);
    Task<User?> GetAsync(long id);
    // Task UpdateAsync(User user);
}
