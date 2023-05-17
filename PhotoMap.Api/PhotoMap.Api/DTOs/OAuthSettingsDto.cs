namespace PhotoMap.Api.DTOs;

public class OAuthSettingsDto
{
    public required string ClientId { get; set; }
    public required string RedirectUri { get; set; }
    public required string ResponseType { get; set; }
    public required string AuthorizeUrl { get; set; }
    public string? TokenUrl { get; set; }
}