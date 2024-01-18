using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhotoMap.Api.Database.Configurations;
using PhotoMap.Api.Database.Entities;

namespace PhotoMap.Api.Database
{
    public class PhotoMapContext : DbContext
    {
        private readonly IConfiguration? _configuration;

        public DbSet<UserEntity> Users { get; set; } = null!;
        public DbSet<PhotoEntity> Photos { get; set; } = null!;
        public DbSet<PhotoSourceEntity> PhotoSources { get; set; } = null!;
        public DbSet<UserPhotoSourceStatusEntity> UserPhotoSourcesStatuses { get; set; } = null!;
        public DbSet<UserPhotoSourceEntity> UserPhotoSources { get; set; } = null!;

        public PhotoMapContext(IConfiguration configuration, DbContextOptions<PhotoMapContext> options)
            : base(options)
        {
            _configuration = configuration;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new UserEntityConfiguration());
            modelBuilder.ApplyConfiguration(new PhotoEntityConfiguration());
            modelBuilder.ApplyConfiguration(new PhotoSourceEntityConfiguration());
            modelBuilder.ApplyConfiguration(new UserPhotoSourceEntityConfiguration());
            modelBuilder.ApplyConfiguration(new UserPhotoSourceStatusEntityConfiguration());
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (_configuration == null || optionsBuilder.IsConfigured)
                return;

            optionsBuilder
                .UseNpgsql(_configuration["ConnectionString"])
                .UseSnakeCaseNamingConvention();
        }
    }
}
