using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoMap.Api.Database.Entities;

namespace PhotoMap.Api.Database.Configurations
{
    public class VideoEntityConfiguration : IEntityTypeConfiguration<VideoEntity>
    {
        public void Configure(EntityTypeBuilder<VideoEntity> builder)
        {
            builder.HasKey(a => a.Id);
            builder.Property(a => a.UserId).IsRequired();
            builder.Property(a => a.PhotoSourceId).IsRequired();
            builder.Property(a => a.ExternalId).IsRequired();
            builder.Property(a => a.FileName).IsRequired();
            builder.Property(a => a.FolderPath);
            builder.Property(a => a.MimeType);
            builder.Property(a => a.Size).IsRequired();
            builder.Property(a => a.DateTimeTaken).IsRequired();
            builder.Property(a => a.ExifDateTime);
            builder.Property(a => a.Latitude);
            builder.Property(a => a.Longitude);
            builder.Property(a => a.PreviewFilePath);
            builder.Property(a => a.PreviewContentType);
            builder.Property(a => a.AddedOn).IsRequired();
            builder.ToTable("videos");

            // file ID assigned by the photo source (Yandex.Disk resource_id)
            builder.HasIndex(a => new { a.UserId, a.PhotoSourceId, a.ExternalId }).IsUnique();
            // the videos of a user are listed by the date they were taken
            builder.HasIndex(a => new { a.UserId, a.DateTimeTaken });

            builder.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId);
            builder.HasOne(a => a.PhotoSource).WithMany().HasForeignKey(a => a.PhotoSourceId);
        }
    }
}
