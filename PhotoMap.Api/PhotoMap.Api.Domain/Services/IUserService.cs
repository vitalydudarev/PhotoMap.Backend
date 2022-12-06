using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Domain.Services
{
    public interface IUserService
    {
        Task AddAsync(string name);

        Task<User?> GetAsync(int id);

        Task UpdateAsync(
            int id,
            string? yandexDiskToken,
            int? yandexDiskTokenExpiresIn,
            ProcessingStatus? yandexDiskStatus,
            string? dropboxToken,
            int? dropboxTokenExpiresIn,
            ProcessingStatus? dropboxStatus);
    }
}
