namespace PhotoMap.Api.DTOs;

public class AuthSettingsDto
{
    public required OAuthConfigurationDto OAuthConfiguration { get; set; }
    public required string RelativeAuthUrl { get; set; }
}