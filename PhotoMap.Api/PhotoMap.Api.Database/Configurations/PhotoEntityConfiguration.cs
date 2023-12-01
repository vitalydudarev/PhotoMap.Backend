using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoMap.Api.Database.Entities;

namespace PhotoMap.Api.Database.Configurations
{
    public class PhotoEntityConfiguration : IEntityTypeConfiguration<PhotoEntity>
    {
        public void Configure(EntityTypeBuilder<PhotoEntity> builder)
        {
            builder.HasKey(a => a.Id);
            builder.Property(a => a.UserId).IsRequired();
            builder.Property(a => a.ThumbnailLargeFilePath);
            builder.Property(a => a.ThumbnailSmallFilePath);
            builder.Property(a => a.FileName).IsRequired();
            builder.Property(a => a.DateTimeTaken).IsRequired();
            builder.Property(a => a.Latitude);
            builder.Property(a => a.Longitude);
            builder.Property(a => a.HasGps).IsRequired();
            builder.Property(a => a.ExifString);
            builder.Property(a => a.PhotoSourceId).IsRequired();
            builder.Property(a => a.Path);
            builder.Property(a => a.AddedOn).IsRequired();
            builder.ToTable("photos");

            builder.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId);
            builder.HasOne(a => a.PhotoSource).WithMany().HasForeignKey(a => a.PhotoSourceId);
        }
    }
}
