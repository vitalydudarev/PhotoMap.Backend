namespace PhotoMap.Api.Services.Exceptions;

/// <summary>
/// A call to the API of a photo source has failed.
/// </summary>
public abstract class PhotoSourceException : Exception
{
    protected PhotoSourceException(string message, bool isAuthError) : base(message)
    {
        IsAuthError = isAuthError;
    }

    /// <summary>
    /// The access token is expired or invalid, every further API call fails as well.
    /// </summary>
    public bool IsAuthError { get; }
}
