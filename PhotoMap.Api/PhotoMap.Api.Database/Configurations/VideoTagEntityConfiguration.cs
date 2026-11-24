using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoMap.Api.Database.Entities;

namespace PhotoMap.Api.Database.Configurations
{
    public class VideoTagEntityConfiguration : IEntityTypeConfiguration<VideoTagEntity>
    {
        public void Configure(EntityTypeBuilder<VideoTagEntity> builder)
        {
            builder.HasKey(a => new { a.VideoId, a.TagId });
            builder.ToTable("video_tags");

            // the videos are filtered by their tags
            builder.HasIndex(a => a.TagId);

            // deleting a video or a tag takes it off the other
            builder.HasOne(a => a.Video).WithMany(a => a.Tags).HasForeignKey(a => a.VideoId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(a => a.Tag).WithMany(a => a.Videos).HasForeignKey(a => a.TagId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
