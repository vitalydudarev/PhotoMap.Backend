using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoMap.Api.Database.Entities;

namespace PhotoMap.Api.Database.Configurations
{
    public class PhotoLocationEntityConfiguration : IEntityTypeConfiguration<PhotoLocationEntity>
    {
        public void Configure(EntityTypeBuilder<PhotoLocationEntity> builder)
        {
            builder.HasKey(a => new { a.PhotoId, a.LocationId });
            builder.ToTable("photo_locations");

            // the photos are filtered by their locations
            builder.HasIndex(a => a.LocationId);

            // deleting a photo takes it off its locations; a location is left behind, and offered again once a photo
            // is taken in it
            builder.HasOne(a => a.Photo).WithMany(a => a.Locations).HasForeignKey(a => a.PhotoId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(a => a.Location).WithMany(a => a.Photos).HasForeignKey(a => a.LocationId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
