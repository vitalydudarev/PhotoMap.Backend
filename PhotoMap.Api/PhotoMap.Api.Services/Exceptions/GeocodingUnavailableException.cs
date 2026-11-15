namespace PhotoMap.Api.Services.Exceptions;

/// <summary>
/// No location can be geocoded for some time: the rate limit of the geocoding API is reached, or the access token is
/// refused. The geocoding is tried again after <see cref="RetryAfter"/>.
/// </summary>
public class GeocodingUnavailableException : Exception
{
    public GeocodingUnavailableException(string message, TimeSpan retryAfter) : base(message)
    {
        RetryAfter = retryAfter;
    }

    public TimeSpan RetryAfter { get; }
}
