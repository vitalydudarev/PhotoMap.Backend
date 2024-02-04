using PhotoMap.Shared.Models;

namespace PhotoMap.Worker.Models
{
    public class YandexDiskFileInfo : DownloadedFileInfo
    {
        // public override string Source { get; set; } = "Yandex.Disk";

        public YandexDiskFileInfo(string resourceName, string path, DateTime? createdOn, byte[] fileContents, string fileId)
            : base(resourceName, path, createdOn, fileId)
        {
        }
    }
}
