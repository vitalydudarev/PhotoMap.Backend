using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Domain.Repositories;

public interface IUserRepository
{
    Task<User?> GetAsync(long id);
}
