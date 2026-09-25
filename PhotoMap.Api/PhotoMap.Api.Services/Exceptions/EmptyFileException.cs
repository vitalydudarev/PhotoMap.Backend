namespace PhotoMap.Api.Services.Exceptions;

/// <summary>
/// A file of a photo source was downloaded without any contents.
/// </summary>
public class EmptyFileException : PhotoSourceException
{
    public EmptyFileException(string message) : base(message, isAuthError: false)
    {
    }
}
