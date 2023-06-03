namespace PhotoMap.Api.Domain.Models;

public class ClientAuthSettings
{
    public required OAuthConfiguration OAuthConfiguration { get; set; }
    public required string RelativeAuthUrl { get; set; }
}