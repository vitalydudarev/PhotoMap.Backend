using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoMap.Api.Database.Entities;

namespace PhotoMap.Api.Database.Configurations
{
    public class CompletedMaintenanceTaskEntityConfiguration : IEntityTypeConfiguration<CompletedMaintenanceTaskEntity>
    {
        public void Configure(EntityTypeBuilder<CompletedMaintenanceTaskEntity> builder)
        {
            builder.HasKey(a => a.Name);
            builder.Property(a => a.CompletedOn).IsRequired();
            builder.ToTable("completed_maintenance_tasks");
        }
    }
}
