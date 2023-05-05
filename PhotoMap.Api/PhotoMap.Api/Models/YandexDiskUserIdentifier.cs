using PhotoMap.Shared;

namespace PhotoMap.Api.Models
{
    public class YandexDiskUserIdentifier : IUserIdentifier
    {
        public long UserId { get; set; }

        public string GetKey()
        {
            return "Yandex.Disk." + UserId;
        }
    }
}
