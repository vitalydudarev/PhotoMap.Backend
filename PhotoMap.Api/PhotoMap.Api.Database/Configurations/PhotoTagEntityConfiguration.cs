using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoMap.Api.Database.Entities;

namespace PhotoMap.Api.Database.Configurations
{
    public class PhotoTagEntityConfiguration : IEntityTypeConfiguration<PhotoTagEntity>
    {
        public void Configure(EntityTypeBuilder<PhotoTagEntity> builder)
        {
            builder.HasKey(a => new { a.PhotoId, a.TagId });
            builder.ToTable("photo_tags");

            // the photos are filtered by their tags
            builder.HasIndex(a => a.TagId);

            // deleting a photo or a tag takes it off the other
            builder.HasOne(a => a.Photo).WithMany(a => a.Tags).HasForeignKey(a => a.PhotoId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(a => a.Tag).WithMany(a => a.Photos).HasForeignKey(a => a.TagId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
