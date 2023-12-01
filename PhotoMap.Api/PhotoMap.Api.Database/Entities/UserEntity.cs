namespace PhotoMap.Api.Database.Entities;

public class UserEntity
{
    public long Id { get; set; }
    public required string Name { get; set; }
    
    public ICollection<UserPhotoSourceAuthEntity>? UserPhotoSourcesAuth { get; set; }
    public ICollection<UserPhotoSourceStatusEntity>? UserPhotoSourcesStatus { get; set; }
}