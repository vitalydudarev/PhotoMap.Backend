using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Repositories;
using PhotoMap.Api.Domain.Services;

namespace PhotoMap.Api.Services.Services.Domain
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;

        public UserService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task AddAsync(string name)
        {
            await _userRepository.AddAsync(name);
        }

        public async Task<User?> GetAsync(long id)
        {
            return await _userRepository.GetAsync(id);
        }

        public async Task UpdateAsync(
            long id,
            string? yandexDiskToken,
            int? yandexDiskTokenExpiresIn,
            ProcessingStatus? yandexDiskStatus,
            string? dropboxToken,
            int? dropboxTokenExpiresIn,
            ProcessingStatus? dropboxStatus)
        {
            var user = await _userRepository.GetAsync(id);
            if (user != null)
            {
                if (!string.IsNullOrEmpty(yandexDiskToken))
                {
                    user.YandexDiskAccessToken = yandexDiskToken;
                }

                if (yandexDiskTokenExpiresIn.HasValue)
                {
                    user.YandexDiskAccessTokenExpiresOn =
                        DateTimeOffset.UtcNow.AddSeconds(yandexDiskTokenExpiresIn.Value);
                }

                if (yandexDiskStatus.HasValue)
                {
                    user.YandexDiskStatus = yandexDiskStatus.Value;
                }

                if (!string.IsNullOrEmpty(dropboxToken))
                {
                    user.DropboxAccessToken = dropboxToken;

                    user.DropboxAccessTokenExpiresOn = dropboxTokenExpiresIn.HasValue
                        ? DateTimeOffset.UtcNow.AddSeconds(dropboxTokenExpiresIn.Value)
                        : DateTimeOffset.MaxValue;
                }

                if (dropboxStatus.HasValue)
                    user.DropboxStatus = dropboxStatus.Value;

                // await _userRepository.UpdateAsync(user);
            }
        }
    }
}
