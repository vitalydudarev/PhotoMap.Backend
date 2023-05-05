using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Domain.Services
{
    public interface IUserService
    {
        Task AddAsync(string name);

        Task<User?> GetAsync(long id);

        Task UpdateAsync(
            long id,
            string? yandexDiskToken,
            int? yandexDiskTokenExpiresIn,
            ProcessingStatus? yandexDiskStatus,
            string? dropboxToken,
            int? dropboxTokenExpiresIn,
            ProcessingStatus? dropboxStatus);
    }
}
