using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Database.Configurations
{
    public class PhotoTypeConfiguration : IEntityTypeConfiguration<Photo>
    {
        public void Configure(EntityTypeBuilder<Photo> builder)
        {
            builder.HasKey(a => a.Id);
            builder.Property(a => a.UserId);
            builder.Property(a => a.PhotoFileId);
            builder.Property(a => a.ThumbnailLargeFilePath);
            builder.Property(a => a.ThumbnailSmallFilePath);
            builder.Property(a => a.FileName);
            builder.Property(a => a.DateTimeTaken);
            builder.Property(a => a.Latitude);
            builder.Property(a => a.Longitude);
            builder.Property(a => a.HasGps);
            builder.Property(a => a.ExifString);
            builder.Property(a => a.Source);
            builder.Property(a => a.Path);
            builder.Property(a => a.AddedOn);
            builder.ToTable("Photos");
            
            builder.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId);
        }
    }
}
