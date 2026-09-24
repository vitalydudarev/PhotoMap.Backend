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

        public async Task<Models.Disk> GetDiskAsync(CancellationToken cancellationToken)
        {
            return await GetAsync<Models.Disk>(Url, cancellationToken);
        }

        /// <param name="sort">The field the embedded resources of a folder are sorted by (name, path, created,
        /// modified or size, "-" before it for descending order), by name when null.</param>
        public async Task<Resource> GetResourceAsync(string path, CancellationToken cancellationToken, int offset = 0, int limit = 20,
            string? sort = null)
        {
            var parameters = new Dictionary<string, string>
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
            var parameters = new Dictionary<string, string>
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

        public async Task<FilesResourceList> GetFlatFilesListAsync(CancellationToken cancellationToken, string mediaType = null, int limit = 20)
        {
            var parameters = new Dictionary<string, string>
            {
                { nameof(mediaType), mediaType },
                { nameof(limit), limit.ToString() }
            };

            var url = _urlBuilder.Build(Url, "resources/files", parameters);

            return await GetAsync<FilesResourceList>(url, cancellationToken);
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

            return await JsonSerializer.DeserializeAsync<T>(responseStream, _jsonSerializerOptions, cancellationToken);
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
