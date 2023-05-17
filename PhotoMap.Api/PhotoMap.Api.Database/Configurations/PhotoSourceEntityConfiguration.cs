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
            builder.Property(a => a.AuthSettings).IsRequired().HasColumnType("jsonb");
            builder.Property(a => a.ServiceImplementationType).IsRequired();
            builder.Property(a => a.SettingsImplementationType).IsRequired();
            builder.ToTable("PhotoSources");
        }
    }
}
