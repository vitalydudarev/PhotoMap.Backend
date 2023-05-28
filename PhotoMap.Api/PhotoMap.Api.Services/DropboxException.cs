namespace PhotoMap.Api.Services
{
    public class DropboxException : Exception
    {
        public DropboxException(string message) : base(message)
        {
        }
    }
}
