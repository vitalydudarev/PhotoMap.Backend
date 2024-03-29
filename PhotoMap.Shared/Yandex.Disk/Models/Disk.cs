namespace PhotoMap.Shared.Yandex.Disk.Models
{
    public class Disk
    {
        public bool UnlimitedAutouploadEnabled { get; set; }
        public long MaxFileSize { get; set; }
        public long TotalSpace { get; set; }
        public long TrashSize { get; set; }
        public bool IsPaid { get; set; }
        public long UsedSpace { get; set; }
        public SystemFolders SystemFolders { get; set; }
        public User User { get; set; }
        public long Revision { get; set; }
    }
}