using PhotoMap.Shared;

namespace PhotoMap.Worker.Models
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
