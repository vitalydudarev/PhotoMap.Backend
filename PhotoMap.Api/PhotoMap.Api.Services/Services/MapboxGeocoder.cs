using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;
using PhotoMap.Api.Services.Exceptions;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// Reverse geocoding by the Mapbox Geocoding API v6. A singleton: the rate limit is of the whole application, every
/// request of it goes through the one limiter.
/// </summary>
public class MapboxGeocoder : IGeocoder, IDisposable
{
    public const string HttpClientName = "Mapbox";

    private const string ReverseGeocodingUrl = "https://api.mapbox.com/search/geocode/v6/reverse";

    /// <summary>
    /// How long the geocoding waits when the token is refused: a new one only comes with a restart, but the refusal may
    /// be of the account for a while.
    /// </summary>
    private static readonly TimeSpan UnauthorizedRetryAfter = TimeSpan.FromHours(1);

    private static readonly TimeSpan DefaultRateLimitRetryAfter = TimeSpan.FromMinutes(1);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MapboxSettings _settings;
    private readonly RateLimiter _rateLimiter;

    public MapboxGeocoder(IHttpClientFactory httpClientFactory, IOptions<MapboxSettings> settings)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;

        // the requests of a minute are spread over its seconds: a second's share each second, so that no minute,
        // however it is counted, gets more than the limit
        var requestsPerSecond = Math.Max(1, _settings.RequestsPerMinute / 60);
        _rateLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = requestsPerSecond,
            TokensPerPeriod = requestsPerSecond,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            QueueLimit = int.MaxValue,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        });
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_settings.AccessToken);

    public async Task<string> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("The Mapbox access token is not configured.");
        }

        using var lease = await _rateLimiter.AcquireAsync(1, cancellationToken);

        var url = string.Create(CultureInfo.InvariantCulture,
            $"{ReverseGeocodingUrl}?longitude={longitude}&latitude={latitude}&language=en" +
            $"&access_token={Uri.EscapeDataString(_settings.AccessToken!)}");

        var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await httpClient.GetAsync(url, cancellationToken);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new GeocodingUnavailableException("The rate limit of the Mapbox Geocoding API is reached",
                GetRateLimitReset(response));
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new GeocodingUnavailableException(
                $"The Mapbox access token is refused ({(int)response.StatusCode})", UnauthorizedRetryAfter);
        }

        // the message has the status, but not the URL, which has the token in it
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <summary>
    /// How long until the rate limit is reset, by the X-Rate-Limit-Reset header (the Unix time it is reset at).
    /// </summary>
    private static TimeSpan GetRateLimitReset(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("X-Rate-Limit-Reset", out var values) &&
            long.TryParse(values.FirstOrDefault(), out var resetAt))
        {
            var retryAfter = DateTimeOffset.FromUnixTimeSeconds(resetAt) - DateTimeOffset.UtcNow;
            if (retryAfter > TimeSpan.Zero && retryAfter <= TimeSpan.FromHours(1))
            {
                // a second more, the clocks may differ
                return retryAfter + TimeSpan.FromSeconds(1);
            }
        }

        return response.Headers.RetryAfter?.Delta ?? DefaultRateLimitRetryAfter;
    }

    public void Dispose()
    {
        _rateLimiter.Dispose();
    }
}
