using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoMap.Api.Database.Entities;
using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Database.Configurations
{
    public class TagEntityConfiguration : IEntityTypeConfiguration<TagEntity>
    {
        public void Configure(EntityTypeBuilder<TagEntity> builder)
        {
            builder.HasKey(a => a.Id);
            builder.Property(a => a.UserId).IsRequired();
            builder.Property(a => a.Name).IsRequired().HasMaxLength(TagNames.MaxLength);
            builder.Property(a => a.NormalizedName).IsRequired().HasMaxLength(TagNames.MaxLength);
            builder.ToTable("tags");

            // a user has a single tag of a name, whatever its case
            builder.HasIndex(a => new { a.UserId, a.NormalizedName }).IsUnique();

            builder.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
