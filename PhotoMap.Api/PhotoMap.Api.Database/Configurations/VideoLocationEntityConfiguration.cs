using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoMap.Api.Database.Entities;

namespace PhotoMap.Api.Database.Configurations
{
    public class VideoLocationEntityConfiguration : IEntityTypeConfiguration<VideoLocationEntity>
    {
        public void Configure(EntityTypeBuilder<VideoLocationEntity> builder)
        {
            builder.HasKey(a => new { a.VideoId, a.LocationId });
            builder.ToTable("video_locations");

            // the videos are filtered by their locations
            builder.HasIndex(a => a.LocationId);

            // deleting a video takes it off its locations; a location is left behind, and offered again once a photo
            // or a video is taken in it
            builder.HasOne(a => a.Video).WithMany(a => a.Locations).HasForeignKey(a => a.VideoId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(a => a.Location).WithMany(a => a.Videos).HasForeignKey(a => a.LocationId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
