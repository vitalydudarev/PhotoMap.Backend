using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhotoMap.Api.Database.Configurations;
using PhotoMap.Api.Database.Entities;

namespace PhotoMap.Api.Database
{
    public class PhotoMapContext : DbContext
    {
        private readonly IConfiguration _configuration;

        public DbSet<User> Users { get; set; }
        public DbSet<Photo> Photos { get; set; }

        public PhotoMapContext(IConfiguration configuration, DbContextOptions<PhotoMapContext> options)
            : base(options)
        {
            _configuration = configuration;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new UserTypeConfiguration());
            modelBuilder.ApplyConfiguration(new PhotoTypeConfiguration());
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (_configuration == null || optionsBuilder.IsConfigured)
                return;

            optionsBuilder.UseNpgsql(_configuration["ConnectionString"]);
        }
    }
}
