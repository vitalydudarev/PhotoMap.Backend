using PhotoMap.Shared.Yandex.Disk.Models;

namespace PhotoMap.Shared.Yandex.Disk
{
    public class ApiException : Exception
    {
        public ApiError ApiError { get; set; }
        
        public ApiException(ApiError apiError) : base($"Yandex API error: {apiError.Description}.")
        {
            ApiError = apiError;
        }
    }
}