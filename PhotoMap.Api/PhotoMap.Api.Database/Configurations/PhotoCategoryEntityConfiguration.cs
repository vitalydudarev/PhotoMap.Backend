using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoMap.Api.Database.Entities;

namespace PhotoMap.Api.Database.Configurations
{
    public class PhotoCategoryEntityConfiguration : IEntityTypeConfiguration<PhotoCategoryEntity>
    {
        public void Configure(EntityTypeBuilder<PhotoCategoryEntity> builder)
        {
            builder.HasKey(a => new { a.PhotoId, a.Category });
            builder.ToTable("photo_categories");

            // deleting the photos deletes their categories along
            builder.HasOne(a => a.Photo).WithMany(a => a.Categories).HasForeignKey(a => a.PhotoId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
