namespace PhotoMap.Shared.Yandex.Disk.Models
{
    public class ResourceList
    {
        public string Sort { get; set; }
        public Resource[] Items { get; set; }
        public int Limit { get; set; }
        public int Offset { get; set; }
        public string Path { get; set; }
        public int Total { get; set; }
    }
}