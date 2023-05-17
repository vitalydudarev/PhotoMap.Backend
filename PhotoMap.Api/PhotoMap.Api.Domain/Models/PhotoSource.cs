namespace PhotoMap.Api.Domain.Models;

public class PhotoSource
{
    public required long Id { get; set; }
    public required string Name { get; set; }
    public required string ServiceSettings { get; set; }
    public required OAuthSettings AuthSettings { get; set; }
    public required string ServiceImplementationType { get; set; }
    public required string SettingsImplementationType { get; set; }
}