using System;

namespace PhotoMap.Api
{
    public class FileInfo
    {
        public long Id { get; set; }
        public string FileName { get; set; } = null!;
        public long Size { get; set; }
        public DateTime AddedOn { get; set; }
    }
}
