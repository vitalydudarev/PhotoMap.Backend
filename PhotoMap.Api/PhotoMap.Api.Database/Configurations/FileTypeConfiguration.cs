using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using File = PhotoMap.Api.Domain.Models.File;

namespace PhotoMap.Api.Database.Configurations
{
    public class FileTypeConfiguration : IEntityTypeConfiguration<File>
    {
        public void Configure(EntityTypeBuilder<File> builder)
        {
            builder.HasKey(a => a.Id);
            builder.Property(a => a.FileName).IsRequired();
            builder.Property(a => a.FullPath);
            builder.Property(a => a.AddedOn);
            builder.Property(a => a.Size);
            builder.ToTable("Files");
        }
    }
}
