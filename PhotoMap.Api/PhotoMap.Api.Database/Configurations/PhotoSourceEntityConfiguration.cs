using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoMap.Api.Database.Entities;

namespace PhotoMap.Api.Database.Configurations
{
    public class PhotoSourceEntityConfiguration : IEntityTypeConfiguration<PhotoSourceEntity>
    {
        public void Configure(EntityTypeBuilder<PhotoSourceEntity> builder)
        {
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Name).IsRequired();
            builder.Property(a => a.Settings).IsRequired();
            builder.Property(a => a.ClientAuthSettings).IsRequired().HasColumnType("jsonb");
            builder.Property(a => a.ServiceFactoryImplementationType).IsRequired();
            builder.ToTable("photo_sources");
        }
    }
}
