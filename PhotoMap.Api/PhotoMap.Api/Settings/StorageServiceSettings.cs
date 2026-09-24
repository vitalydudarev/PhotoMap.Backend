namespace PhotoMap.Api.Settings
{
    public class StorageServiceSettings
    {
        public string ApiUrl { get; set; } = null!;
        public string GetFileEndpoint { get; set; } = null!;
        public string DeleteAllFilesEndpoint { get; set; } = null!;
    }
}
