using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoMap.Api.Database.Entities;

namespace PhotoMap.Api.Database.Configurations
{
    public class UserPhotoSourceStatusEntityConfiguration : IEntityTypeConfiguration<UserPhotoSourceStatusEntity>
    {
        public void Configure(EntityTypeBuilder<UserPhotoSourceStatusEntity> builder)
        {
            builder.HasKey(a => new { a.UserId, a.PhotoSourceId });
            builder.Property(a => a.UserId).IsRequired();
            builder.Property(a => a.PhotoSourceId).IsRequired();
            builder.Property(a => a.TotalCount);
            builder.Property(a => a.ProcessedCount);
            builder.Property(a => a.FailedCount);
            builder.Property(a => a.LastProcessedFileIndex);
            builder.Property(a => a.LastUpdatedAt);
            builder.ToTable("users_photo_sources_status");

            builder
                .HasOne(a => a.User)
                .WithMany(b => b.UserPhotoSourcesStatus)
                .HasForeignKey(a => a.UserId);
            
            builder
                .HasOne(a => a.PhotoSource)
                .WithMany(b => b.UserPhotoSourcesStatus)
                .HasForeignKey(a => a.PhotoSourceId);
        }
    }
}
