using System.Runtime.CompilerServices;
using PhotoMap.Shared;
using PhotoMap.Shared.Yandex.Disk;
using PhotoMap.Shared.Yandex.Disk.Models;
using PhotoMap.Worker.Models;
using PhotoMap.Worker.Services.Definitions;

namespace PhotoMap.Worker.Services.Implementations
{
    public class YandexDiskDownloadService : IYandexDiskDownloadService, IDisposable
    {
        private static readonly HttpClient Client = new HttpClient();
        private readonly IProgressReporter _progressReporter;
        private readonly IYandexDiskDownloadStateService _yandexDiskDownloadStateService;
        private readonly ILogger<YandexDiskDownloadService> _logger;
        private int _currentOffset;
        private int _totalFiles;
        private YandexDiskData _data;

        private const int Limit = 100;

        public YandexDiskDownloadService(
            IProgressReporter progressReporter,
            IYandexDiskDownloadStateService yandexDiskDownloadStateService,
            ILogger<YandexDiskDownloadService> logger)
        {
            _progressReporter = progressReporter;
            _yandexDiskDownloadStateService = yandexDiskDownloadStateService;
            _logger = logger;
        }

        public async IAsyncEnumerable<YandexDiskFileInfo> DownloadFilesAsync(
            IUserIdentifier userIdentifier,
            string accessToken,
            [EnumeratorCancellation] CancellationToken cancellationToken,
            StoppingAction stoppingAction)
        {
            var apiClient = new ApiClient(accessToken, Client);

            _data = _yandexDiskDownloadStateService.GetData(userIdentifier.UserId);
            
            if (_data == null)
            {
                _data = new YandexDiskData { UserId = userIdentifier.UserId, YandexDiskAccessToken = accessToken };
            }
            else
            {
                _currentOffset = _data.CurrentIndex;
            }

            var disk = await WrapApiCallAsync(() => apiClient.GetDiskAsync(cancellationToken));

            bool firstIteration = true;

            while (_currentOffset <= _totalFiles || firstIteration)
            {
                var resource =
                    await WrapApiCallAsync(() =>
                        apiClient.GetResourceAsync(disk.SystemFolders.Photostream, cancellationToken, _currentOffset, Limit));

                _totalFiles = resource.Embedded.Total;

                var items = resource.Embedded.Items;
                if (items != null && items.Length > 0)
                {
                    foreach (var item in items)
                    {
                        if (cancellationToken.IsCancellationRequested || stoppingAction.IsStopRequested)
                        {
                            _logger.LogInformation("Cancellation requested.");
                            yield break;
                        }

                        if (item.MediaType == "video")
                        {
                            _currentOffset++;
                            continue;
                        }

                        var entity = await DownloadAsync(item, disk);
                        if (entity == null)
                            yield break;

                        _currentOffset++;

                        _progressReporter.Report(userIdentifier, _currentOffset, _totalFiles);

                        yield return entity;
                    }
                }

                firstIteration = false;
            }
        }

        public void Dispose()
        {
            _logger.LogInformation("Yandex.Disk Download Service disposed.");
            _logger.LogInformation("Current offset: " + _currentOffset);

            SaveData();
        }

        private async Task<YandexDiskFileInfo> DownloadAsync(Resource resource, Disk disk)
        {
            _logger.LogInformation($"Started downloading {resource.Name}.");

            try
            {
                var bytes = await Client.GetByteArrayAsync(resource.File);
                var createdOn = resource.Exif != null ? resource.Exif.DateTime : resource.PhotosliceTime;

                var downloadedFileInfo = new YandexDiskFileInfo(resource.Name, resource.Path, createdOn, bytes, resource.ResourceId);

                _logger.LogInformation($"Finished downloading {resource.Name}.");

                return downloadedFileInfo;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error downloading {resource.Name}. {e.Message}");
            }

            return null;
        }

        private void SaveData()
        {
            _data.CurrentIndex = _currentOffset;
            _data.TotalPhotos = _totalFiles;

            _yandexDiskDownloadStateService.SaveData(_data);
        }

        private async Task<T> WrapApiCallAsync<T>(Func<Task<T>> apiCall)
        {
            try
            {
                return await apiCall();
            }
            catch (Exception e)
            {
                _logger.LogError("Yandex.Disk: " + e.Message);

                SaveData();

                throw new YandexDiskException(e.Message);
            }
        }
    }
}
