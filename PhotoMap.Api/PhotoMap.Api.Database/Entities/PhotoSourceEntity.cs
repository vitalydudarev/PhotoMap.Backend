using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Database.Entities;

public class PhotoSourceEntity
{
    public required long Id { get; set; }
    public required string Name { get; set; }
    public required string Settings { get; set; }
    public required OAuthSettings AuthSettings { get; set; }
    public required string ServiceImplementationType { get; set; }
    public required string SettingsImplementationType { get; set; }
    
    public ICollection<UserPhotoSourceEntity> UserPhotoSources { get; set; }
}