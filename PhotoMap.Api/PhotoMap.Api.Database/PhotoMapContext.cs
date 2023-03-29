using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhotoMap.Api.Database.Configurations;
using PhotoMap.Api.Domain.Models;
using File = PhotoMap.Api.Domain.Models.File;

namespace PhotoMap.Api.Database
{
    public class PhotoMapContext : DbContext
    {
        private readonly IConfiguration _configuration;

        public DbSet<User> Users { get; set; }
        public DbSet<Photo> Photos { get; set; }
        // TODO: delete?
        // public DbSet<File> Files { get; set; }

        public PhotoMapContext(IConfiguration configuration, DbContextOptions<PhotoMapContext> options)
            : base(options)
        {
            _configuration = configuration;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new UserTypeConfiguration());
            modelBuilder.ApplyConfiguration(new PhotoTypeConfiguration());
            modelBuilder.ApplyConfiguration(new FileTypeConfiguration());
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (_configuration == null || optionsBuilder.IsConfigured)
                return;

            optionsBuilder.UseNpgsql(_configuration["ConnectionString"]);
        }
    }
}
