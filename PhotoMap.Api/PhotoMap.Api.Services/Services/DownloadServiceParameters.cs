using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Services.Services;

public class DownloadServiceParameters
{
    public long SourceId { get; set; }
    public long UserId { get; set; }
    public UserAuthResult AuthResult { get; set; } = null!;
    /// <summary>
    /// OAuth client ID of the photo source, needed to refresh access tokens.
    /// </summary>
    public string ClientId { get; set; } = null!;
    public ProcessingProgress Progress { get; set; } = null!;
}