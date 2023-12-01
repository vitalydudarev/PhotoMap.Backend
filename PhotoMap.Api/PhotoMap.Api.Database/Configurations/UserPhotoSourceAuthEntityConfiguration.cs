using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoMap.Api.Database.Entities;

namespace PhotoMap.Api.Database.Configurations
{
    public class UserPhotoSourceAuthEntityConfiguration : IEntityTypeConfiguration<UserPhotoSourceAuthEntity>
    {
        public void Configure(EntityTypeBuilder<UserPhotoSourceAuthEntity> builder)
        {
            builder.HasKey(a => new { a.UserId, a.PhotoSourceId });
            builder.Property(a => a.UserId).IsRequired();
            builder.Property(a => a.PhotoSourceId).IsRequired();
            builder.Property(a => a.UserAuthResult).HasColumnType("jsonb");
            builder.ToTable("users_photo_sources_auth");

            builder
                .HasOne(a => a.User)
                .WithMany(b => b.UserPhotoSourcesAuth)
                .HasForeignKey(a => a.UserId);
            
            builder
                .HasOne(a => a.PhotoSource)
                .WithMany(b => b.UserPhotoSourcesAuth)
                .HasForeignKey(a => a.PhotoSourceId);
        }
    }
}
