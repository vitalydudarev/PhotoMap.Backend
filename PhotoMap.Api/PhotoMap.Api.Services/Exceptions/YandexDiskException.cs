namespace PhotoMap.Api.Services.Exceptions;

public class YandexDiskException : PhotoSourceException
{
    public YandexDiskException(string message, bool isAuthError = false) : base(message, isAuthError)
    {
    }
}
