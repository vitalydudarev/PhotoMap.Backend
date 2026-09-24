namespace PhotoMap.Shared.Yandex.Disk.Models
{
    public class Link
    {
        public string Href { get; set; } = null!;
        public string Method { get; set; } = null!;
        public bool Templated { get; set; }
    }
}
