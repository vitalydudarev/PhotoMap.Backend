using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhotoMap.Api.Database.Configurations;
using PhotoMap.Api.Database.Entities;

namespace PhotoMap.Api.Database
{
    public class PhotoMapContext : DbContext
    {
        private readonly IConfiguration? _configuration;

        public DbSet<UserEntity> Users { get; set; }
        public DbSet<PhotoEntity> Photos { get; set; }
        public DbSet<PhotoSourceEntity> PhotoSources { get; set; }
        public DbSet<UserPhotoSourceAuthEntity> UserPhotoSourcesAuth { get; set; }
        public DbSet<UserPhotoSourceStatusEntity> UserPhotoSourcesStatus { get; set; }

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
            modelBuilder.ApplyConfiguration(new UserPhotoSourceAuthEntityConfiguration());
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
