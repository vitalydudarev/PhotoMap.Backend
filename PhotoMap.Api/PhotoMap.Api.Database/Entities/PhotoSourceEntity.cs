using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Database.Entities;

public class PhotoSourceEntity
{
    public required long Id { get; set; }
    public required string Name { get; set; }
    public required string Settings { get; set; }
    public required AuthSettings AuthSettings { get; set; }
    public required string ServiceFactoryImplementationType { get; set; }
    
    public ICollection<UserPhotoSourceEntity> UserPhotoSources { get; set; }
}