using PhotoMap.Shared;

namespace PhotoMap.Api.Models
{
    public class YandexDiskUserIdentifier : IUserIdentifier
    {
        public int UserId { get; set; }

        public string GetKey()
        {
            return "Yandex.Disk." + UserId;
        }
    }
}
