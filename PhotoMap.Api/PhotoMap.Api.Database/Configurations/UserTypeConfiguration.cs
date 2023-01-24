using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoMap.Api.Database.Entities;
using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Database.Configurations
{
    public class UserTypeConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Name).IsRequired();
            builder.Property(a => a.YandexDiskAccessToken);
            builder.Property(a => a.YandexDiskAccessTokenExpiresOn);
            builder.Property(a => a.YandexDiskStatus);
            builder.Property(a => a.DropboxAccessToken);
            builder.Property(a => a.DropboxAccessTokenExpiresOn);
            builder.Property(a => a.DropboxStatus);
            builder.ToTable("Users");
        }
    }
}
