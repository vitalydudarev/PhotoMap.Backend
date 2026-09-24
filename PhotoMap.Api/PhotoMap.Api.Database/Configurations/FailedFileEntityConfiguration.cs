using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoMap.Api.Database.Entities;

namespace PhotoMap.Api.Database.Configurations
{
    public class FailedFileEntityConfiguration : IEntityTypeConfiguration<FailedFileEntity>
    {
        public void Configure(EntityTypeBuilder<FailedFileEntity> builder)
        {
            builder.HasKey(a => a.Id);
            builder.Property(a => a.UserId).IsRequired();
            builder.Property(a => a.PhotoSourceId).IsRequired();
            builder.Property(a => a.ExternalId).IsRequired();
            builder.Property(a => a.Path);
            builder.Property(a => a.FileName).IsRequired();
            builder.Property(a => a.Stage).IsRequired();
            builder.Property(a => a.Error).IsRequired();
            builder.Property(a => a.Attempts).IsRequired();
            builder.Property(a => a.FailedAt).IsRequired();
            builder.ToTable("failed_files");

            // a file fails again as the same row, its attempts counted
            builder.HasIndex(a => new { a.UserId, a.PhotoSourceId, a.ExternalId }).IsUnique();

            builder.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId);
            builder.HasOne(a => a.PhotoSource).WithMany().HasForeignKey(a => a.PhotoSourceId);
        }
    }
}
