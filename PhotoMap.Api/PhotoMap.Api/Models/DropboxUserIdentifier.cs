using PhotoMap.Shared;

namespace PhotoMap.Api.Models
{
    public class DropboxUserIdentifier : IUserIdentifier
    {
        public long UserId { get; set; }

        public string GetKey()
        {
            return "Dropbox." + UserId;
        }
    }
}
