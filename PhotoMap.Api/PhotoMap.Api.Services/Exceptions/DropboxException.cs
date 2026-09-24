namespace PhotoMap.Api.Services.Exceptions;

public class DropboxException : PhotoSourceException
{
    public DropboxException(string message, bool isAuthError = false) : base(message, isAuthError)
    {
    }
}
