namespace PhotoMap.Worker.Models
{
    public class YandexDiskFileInfo : DownloadedFileInfo
    {
        public override string Source { get; set; } = "Yandex.Disk";

        public YandexDiskFileInfo(string resourceName, string path, DateTime? createdOn, string userName,
            byte[] fileContents, long photoSourceId)
            : base(resourceName, path, createdOn, userName, fileContents, photoSourceId)
        {
        }
    }
}
