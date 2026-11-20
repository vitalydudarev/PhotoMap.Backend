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
            builder.Property(a => a.HorizontalPositioningError);
            builder.Property(a => a.HasGps).IsRequired();
            builder.Property(a => a.ExifString);
            builder.Property(a => a.PhotoSourceId).IsRequired();
            builder.Property(a => a.Path);
            // the path up to its last slash, with the slash kept at the root of the disk
            builder.Property(a => a.FolderPath)
                .HasComputedColumnSql(
                    @"CASE WHEN strpos(path, '/') = 0 THEN NULL ELSE regexp_replace(regexp_replace(path, '/[^/]*$', ''), '(^|:)$', '\1/') END",
                    stored: true);
            builder.Property(a => a.AddedOn).IsRequired();
            builder.Property(a => a.ExternalId);
            builder.Property(a => a.ContentHash);
            builder.Property(a => a.CategoriesVersion).IsRequired();
            builder.Property(a => a.DeletedOn);
            builder.Property(a => a.DuplicateGroupId);
            builder.Property(a => a.FromOtherUploads).IsRequired();
            builder.Property(a => a.Geocoding).HasColumnType("jsonb");
            builder.ToTable("photos");

            // file ID assigned by the photo source (Dropbox file ID, Yandex.Disk resource_id)
            builder.HasIndex(a => new { a.UserId, a.PhotoSourceId, a.ExternalId }).IsUnique();
            // SHA-256 of the file contents, the same across photo sources
            builder.HasIndex(a => new { a.UserId, a.ContentHash });
            // the photos still to be put in their categories, looked for whenever photos are saved
            builder.HasIndex(a => a.CategoriesVersion);
            // the duplicates of a user are listed by their groups
            builder.HasIndex(a => new { a.UserId, a.DuplicateGroupId });
            // the photos of a user are narrowed down to their folders, which are listed to pick from
            builder.HasIndex(a => new { a.UserId, a.FolderPath });
            // the photos still to be geocoded, looked for whenever photos are saved
            builder.HasIndex(a => a.Id)
                .HasDatabaseName("ix_photos_not_geocoded")
                .HasFilter("has_gps AND geocoding IS NULL");

            builder.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId);
            builder.HasOne(a => a.PhotoSource).WithMany().HasForeignKey(a => a.PhotoSourceId);
        }
    }
}
