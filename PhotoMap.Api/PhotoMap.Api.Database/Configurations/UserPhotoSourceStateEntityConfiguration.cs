using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoMap.Api.Database.Entities;

namespace PhotoMap.Api.Database.Configurations
{
    public class UserPhotoSourceStateEntityConfiguration : IEntityTypeConfiguration<UserPhotoSourceStateEntity>
    {
        public void Configure(EntityTypeBuilder<UserPhotoSourceStateEntity> builder)
        {
            builder.HasKey(a => new { a.UserId, a.PhotoSourceId });
            builder.Property(a => a.UserId).IsRequired();
            builder.Property(a => a.PhotoSourceId).IsRequired();
            builder.Property(a => a.State);
            builder.ToTable("users_photo_sources_states");

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
