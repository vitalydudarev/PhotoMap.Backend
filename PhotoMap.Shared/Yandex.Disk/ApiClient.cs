using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;
using PhotoMap.Shared.Yandex.Disk.Models;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace PhotoMap.Shared.Yandex.Disk
{
    public class ApiClient
    {
        private const string Url = "https://cloud-api.yandex.net/v1/disk";

        /// <summary>
        /// How long <see cref="OpenDownloadAsync"/> waits for the download to start: for the download URL and the
        /// headers of the response. The contents are read by the caller, as long as they take.
        /// </summary>
        public static readonly TimeSpan DefaultOpenDownloadTimeout = TimeSpan.FromSeconds(30);

        private readonly UrlBuilder _urlBuilder;
        private readonly HttpClient _httpClient;

        private readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
            { PropertyNamingPolicy = new SnakeCaseNamingPolicy() };

        public ApiClient(string oAuthToken, HttpClient client)
        {
            _httpClient = client;
            _httpClient.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse($"OAuth {oAuthToken}");
            _urlBuilder = new UrlBuilder(NamingConvention.SnakeCase);
        }

        /// <summary>
        /// How long <see cref="OpenDownloadAsync"/> waits for the download to start. The HTTP client is shared with
        /// the other calls, so the timeout is the call's own rather than the client's.
        /// </summary>
        public TimeSpan OpenDownloadTimeout { get; init; } = DefaultOpenDownloadTimeout;

        public async Task<Models.Disk> GetDiskAsync(CancellationToken cancellationToken)
        {
            return await GetAsync<Models.Disk>(Url, cancellationToken);
        }

        /// <param name="sort">The field the embedded resources of a folder are sorted by (name, path, created,
        /// modified or size, "-" before it for descending order), by name when null.</param>
        public async Task<Resource> GetResourceAsync(string path, CancellationToken cancellationToken, int offset = 0, int limit = 20,
            string? sort = null)
        {
            var parameters = new Dictionary<string, string?>
            {
                { nameof(path), path },
                { nameof(offset), offset.ToString() },
                { nameof(limit), limit.ToString() },
                { nameof(sort), sort }
            };

            var url = _urlBuilder.Build(Url, "resources", parameters);

            return await GetAsync<Resource>(url, cancellationToken);
        }

        public async Task<DownloadUrl> GetDownloadUrlAsync(string path, CancellationToken cancellationToken)
        {
            var parameters = new Dictionary<string, string?>
            {
                { nameof(path), path }
            };

            var url = _urlBuilder.Build(Url, "resources/download", parameters);

            return await GetAsync<DownloadUrl>(url, cancellationToken);
        }

        public async Task<byte[]> DownloadFileAsync(string path, CancellationToken cancellationToken)
        {
            var downloadUrl = await GetDownloadUrlAsync(path, cancellationToken);

            using var responseMessage = await _httpClient.GetAsync(downloadUrl.Href, cancellationToken);

            if (!responseMessage.IsSuccessStatusCode)
                throw await CreateApiExceptionAsync(responseMessage, cancellationToken);

            return await responseMessage.Content.ReadAsByteArrayAsync(cancellationToken);
        }

        /// <param name="mediaType">The media types of the files to list, e.g. "image" or "video", every file when null.</param>
        /// <summary>
        /// Starts downloading the file, or the range of it asked for, by the URL its path gets, as
        /// <see cref="DownloadFileAsync"/> does, without reading it: the caller reads the contents as they come,
        /// and disposes the response.
        /// </summary>
        /// <param name="range">The bytes to download, the whole file when null.</param>
        /// <exception cref="TimeoutException">The download has not started within <see cref="OpenDownloadTimeout"/>.</exception>
        public async Task<HttpResponseMessage> OpenDownloadAsync(string path, RangeHeaderValue? range, CancellationToken cancellationToken)
        {
            using var timeoutCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCancellationTokenSource.CancelAfter(OpenDownloadTimeout);

            var timeoutCancellationToken = timeoutCancellationTokenSource.Token;

            try
            {
                var downloadUrl = await GetDownloadUrlAsync(path, timeoutCancellationToken);

                using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl.Href);
                request.Headers.Range = range;

                var responseMessage = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
                    timeoutCancellationToken);

                if (!responseMessage.IsSuccessStatusCode)
                {
                    using (responseMessage)
                    {
                        throw await CreateApiExceptionAsync(responseMessage, timeoutCancellationToken);
                    }
                }

                return responseMessage;
            }
            catch (OperationCanceledException e) when (!cancellationToken.IsCancellationRequested)
            {
                // the caller has not cancelled, the timeout has run out
                throw new TimeoutException($"The download of {path} has not started within {OpenDownloadTimeout.TotalSeconds:0} seconds.", e);
            }
        }

        public async Task<FilesResourceList> GetFlatFilesListAsync(CancellationToken cancellationToken, string? mediaType = null, int limit = 20,
            int offset = 0)
        {
            var parameters = new Dictionary<string, string?>
            {
                { nameof(mediaType), mediaType },
                { nameof(limit), limit.ToString() },
                { nameof(offset), offset.ToString() }
            };

            var url = _urlBuilder.Build(Url, "resources/files", parameters);

            return await GetAsync<FilesResourceList>(url, cancellationToken);
        }

        /// <summary>
        /// Downloads a file by a URL the API has given, such as the URL of a preview of a resource, which is only
        /// served with the OAuth token.
        /// </summary>
        /// <returns>The contents of the file and their media type, as the server sent them.</returns>
        public async Task<(byte[] Contents, string? ContentType)> DownloadByUrlAsync(string url, CancellationToken cancellationToken)
        {
            using var responseMessage = await _httpClient.GetAsync(url, cancellationToken);

            if (!responseMessage.IsSuccessStatusCode)
                throw await CreateApiExceptionAsync(responseMessage, cancellationToken);

            var contents = await responseMessage.Content.ReadAsByteArrayAsync(cancellationToken);

            return (contents, responseMessage.Content.Headers.ContentType?.MediaType);
        }

        public async Task UploadFileAsync(string path, byte[] fileContents, CancellationToken cancellationToken)
        {
            using (var fileContent = new ByteArrayContent(fileContents))
            {
                var url = Url + "/resources/upload?path=" + HttpUtility.UrlEncode(path);
                var link = await GetAsync<Link>(url, cancellationToken);

                var response = await _httpClient.PutAsync(link.Href, fileContent, cancellationToken);
                if (response.StatusCode != HttpStatusCode.Created && response.StatusCode != HttpStatusCode.Accepted)
                    throw new Exception("Error during uploading file.");
            }
        }

        private async Task<T> GetAsync<T>(string url, CancellationToken cancellationToken)
        {
            using var responseMessage = await _httpClient.GetAsync(url, cancellationToken);

            if (responseMessage.StatusCode != HttpStatusCode.OK)
                throw await CreateApiExceptionAsync(responseMessage, cancellationToken);

            await using var responseStream = await responseMessage.Content.ReadAsStreamAsync(cancellationToken);

            return await JsonSerializer.DeserializeAsync<T>(responseStream, _jsonSerializerOptions, cancellationToken)
                   ?? throw new JsonException($"The response of {url} is empty.");
        }

        private async Task<ApiException> CreateApiExceptionAsync(HttpResponseMessage responseMessage, CancellationToken cancellationToken)
        {
            var retryAfter = responseMessage.Headers.RetryAfter?.Delta;
            var content = await responseMessage.Content.ReadAsStringAsync(cancellationToken);

            ApiError? error = null;

            try
            {
                error = JsonSerializer.Deserialize<ApiError>(content, _jsonSerializerOptions);
            }
            catch (JsonException)
            {
                // not every error comes with a JSON body, e.g. the ones of the download server
            }

            error ??= new ApiError { Error = responseMessage.StatusCode.ToString(), Description = responseMessage.ReasonPhrase };

            return new ApiException(error, responseMessage.StatusCode, retryAfter);
        }
    }
}
