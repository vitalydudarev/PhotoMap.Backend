using PhotoMap.Worker.Models;

namespace PhotoMap.Worker.Services.Definitions
{
    public interface IYandexDiskDownloadStateService
    {
        YandexDiskData GetData(long userId);

        void SaveData(YandexDiskData data);
    }
}
