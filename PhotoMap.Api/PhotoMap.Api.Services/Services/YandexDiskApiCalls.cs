using System.Net;
using Microsoft.Extensions.Logging;
using PhotoMap.Api.Services.Exceptions;
using PhotoMap.Shared.Yandex.Disk;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// How the calls to the Yandex.Disk API are made, by the runs of its photos and of its videos alike.
/// </summary>
public static class YandexDiskApiCalls
{
    private const int MaxRetries = 3;

    /// <summary>
    /// Makes the call, again up to <see cref="MaxRetries"/> times when it fails, whatever it fails with, and turns
    /// its errors into <see cref="YandexDiskException"/>. A token Yandex.Disk no longer accepts fails at once, as
    /// does a cancelled run: neither would go any differently on a retry.
    /// </summary>
    /// <param name="retryDelay">How long to wait before the retry of the attempt given, 2, 4 then 8 seconds when
    /// not given. A rate limit reached waits as long as Yandex.Disk asks for instead, when it does.</param>
    public static async Task<T> WrapAsync<T>(ILogger logger, Func<Task<T>> apiCall, CancellationToken cancellationToken = default,
        Func<int, TimeSpan>? retryDelay = null)
    {
        retryDelay ??= attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt));

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await apiCall();
            }
            catch (ApiException e) when (e.StatusCode == HttpStatusCode.Unauthorized)
            {
                logger.LogError(e, "An auth error has occurred while calling API");

                throw new YandexDiskException("An auth error has occurred while calling API: " + e.Message, isAuthError: true);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e) when (attempt <= MaxRetries)
            {
                // without this the file would be counted as failed and skipped until the next run
                var delay = e is ApiException { StatusCode: HttpStatusCode.TooManyRequests, RetryAfter: { } retryAfter }
                    ? retryAfter
                    : retryDelay(attempt);

                logger.LogWarning(e, "Calling API failed: {ErrorMessage}, retrying in {RetryDelay} (attempt {Attempt} of {MaxAttempts})",
                    e.Message, delay, attempt, MaxRetries);

                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception e)
            {
                logger.LogError(e, "An error has occurred while calling API");

                throw new YandexDiskException("An error has occurred while calling API: " + e.Message);
            }
        }
    }
}
