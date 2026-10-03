using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoMap.Api.Database.Entities;

namespace PhotoMap.Api.Database.Configurations
{
    public class UserVideoSourceStatusEntityConfiguration : IEntityTypeConfiguration<UserVideoSourceStatusEntity>
    {
        public void Configure(EntityTypeBuilder<UserVideoSourceStatusEntity> builder)
        {
            builder.HasKey(a => new { a.UserId, a.PhotoSourceId });
            builder.Property(a => a.UserId).IsRequired();
            builder.Property(a => a.PhotoSourceId).IsRequired();
            builder.Property(a => a.TotalCount);
            builder.Property(a => a.ProcessedCount);
            builder.Property(a => a.FailedCount);
            builder.Property(a => a.LastUpdatedAt);
            builder.ToTable("users_video_sources_status");

            builder.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId);
            builder.HasOne(a => a.PhotoSource).WithMany().HasForeignKey(a => a.PhotoSourceId);
        }
    }
}
