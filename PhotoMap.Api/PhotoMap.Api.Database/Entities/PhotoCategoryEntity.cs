using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Database.Entities;

public class PhotoCategoryEntity
{
    public long PhotoId { get; set; }
    public PhotoEntity? Photo { get; set; }
    public PhotoCategory Category { get; set; }
}
