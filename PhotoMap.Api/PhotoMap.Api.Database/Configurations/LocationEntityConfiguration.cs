using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoMap.Api.Database.Entities;

namespace PhotoMap.Api.Database.Configurations
{
    public class LocationEntityConfiguration : IEntityTypeConfiguration<LocationEntity>
    {
        public void Configure(EntityTypeBuilder<LocationEntity> builder)
        {
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Level).IsRequired();
            builder.Property(a => a.Key).IsRequired();
            builder.Property(a => a.Name).IsRequired();
            builder.Property(a => a.Description);
            builder.ToTable("locations");

            // a location is saved once, whichever photo is geocoded first; the same Mapbox ID may be a city and a
            // region both
            builder.HasIndex(a => new { a.Level, a.Key }).IsUnique();
        }
    }
}
