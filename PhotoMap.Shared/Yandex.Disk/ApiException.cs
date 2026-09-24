using System.Net;
using PhotoMap.Shared.Yandex.Disk.Models;

namespace PhotoMap.Shared.Yandex.Disk
{
    public class ApiException : Exception
    {
        public ApiError ApiError { get; set; }

        public HttpStatusCode StatusCode { get; }

        /// <summary>
        /// How long to wait before calling again, when the API has said so (with 429 Too Many Requests).
        /// </summary>
        public TimeSpan? RetryAfter { get; }

        public ApiException(ApiError apiError, HttpStatusCode statusCode, TimeSpan? retryAfter = null)
            : base($"Yandex API error: {apiError.Description}.")
        {
            ApiError = apiError;
            StatusCode = statusCode;
            RetryAfter = retryAfter;
        }
    }
}
