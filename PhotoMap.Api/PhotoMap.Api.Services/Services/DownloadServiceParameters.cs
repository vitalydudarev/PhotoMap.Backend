namespace PhotoMap.Api.Services.Services;

public class DownloadServiceParameters
{
    public long SourceId { get; set; }
    public long UserId { get; set; }
    public string Token { get; set; } = null!;
}