using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoMap.Api.Database.Entities;
using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Database.Configurations
{
    public class FailedDownloadEntityConfiguration : IEntityTypeConfiguration<FailedDownloadEntity>
    {
        public void Configure(EntityTypeBuilder<FailedDownloadEntity> builder)
        {
            builder.HasKey(a => a.Id);
            builder.Property(a => a.UserId).IsRequired();
            builder.Property(a => a.PhotoSourceId).IsRequired();
            builder.Property(a => a.Kind).IsRequired().HasDefaultValue(SourceDownloadKind.ProcessingFolder);
            builder.Property(a => a.Folder).IsRequired().HasDefaultValue("");
            builder.Property(a => a.FileName).IsRequired();
            builder.Property(a => a.Path).IsRequired();
            builder.Property(a => a.Size).IsRequired();
            builder.Property(a => a.Content).IsRequired();
            builder.Property(a => a.Error).IsRequired();
            builder.Property(a => a.Attempts).IsRequired();
            builder.Property(a => a.FailedAt).IsRequired();
            builder.ToTable("failed_downloads");

            // a file fails again as the same row, its attempts counted; a file is known by its folder and name
            builder.HasIndex(a => new { a.UserId, a.PhotoSourceId, a.Kind, a.Folder, a.FileName }).IsUnique();

            builder.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId);
            builder.HasOne(a => a.PhotoSource).WithMany().HasForeignKey(a => a.PhotoSourceId);
        }
    }
}
