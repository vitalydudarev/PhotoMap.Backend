namespace PhotoMap.Api.Services.Exceptions;

public class YandexDiskException : Exception
{
    public YandexDiskException(string message, bool isAuthError = false) : base(message)
    {
        IsAuthError = isAuthError;
    }

    /// <summary>
    /// The access token is expired or invalid, every further API call fails as well.
    /// </summary>
    public bool IsAuthError { get; }
}
