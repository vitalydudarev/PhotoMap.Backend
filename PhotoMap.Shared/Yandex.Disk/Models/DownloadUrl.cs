namespace PhotoMap.Shared.Yandex.Disk.Models
{
    public class DownloadUrl
    {
        public string Href { get; set; } = null!;
        public string Method { get; set; } = null!;
        public bool Templated { get; set; }
    }
}
