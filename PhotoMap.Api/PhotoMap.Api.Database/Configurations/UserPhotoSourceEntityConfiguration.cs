using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoMap.Api.Database.Entities;

namespace PhotoMap.Api.Database.Configurations
{
    public class UserPhotoSourceEntityConfiguration : IEntityTypeConfiguration<UserPhotoSourceEntity>
    {
        public void Configure(EntityTypeBuilder<UserPhotoSourceEntity> builder)
        {
            builder.HasKey(a => new { a.UserId, a.PhotoSourceId });
            builder.Property(a => a.UserId).IsRequired();
            builder.Property(a => a.PhotoSourceId).IsRequired();
            builder.Property(a => a.UserAuthResult).HasColumnType("jsonb");
            builder.Property(a => a.ProcessingState);
            builder.ToTable("users_photo_sources");

            builder
                .HasOne(a => a.User)
                .WithMany(b => b.UserPhotoSourcesStates)
                .HasForeignKey(a => a.UserId);
            
            builder
                .HasOne(a => a.PhotoSource)
                .WithMany(b => b.UserPhotoSourcesStates)
                .HasForeignKey(a => a.PhotoSourceId);
        }
    }
}
