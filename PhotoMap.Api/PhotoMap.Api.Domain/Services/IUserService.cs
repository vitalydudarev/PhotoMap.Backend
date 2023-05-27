using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Domain.Services
{
    public interface IUserService
    {
        Task<User?> GetAsync(long id);
    }
}
