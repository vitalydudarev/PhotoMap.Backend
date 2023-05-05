namespace PhotoMap.Api.Domain.Models;

public class User
{
    public long Id { get; set; }
    public string Name { get; set; }
    public string? YandexDiskAccessToken { get; set; }
    public DateTimeOffset? YandexDiskAccessTokenExpiresOn { get; set; }
    public ProcessingStatus? YandexDiskStatus { get; set; }
    public string? DropboxAccessToken { get; set; }
    public DateTimeOffset? DropboxAccessTokenExpiresOn { get; set; }
    public ProcessingStatus? DropboxStatus { get; set; }
}
