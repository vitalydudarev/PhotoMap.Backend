using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using PhotoMap.Api.Database.Configurations;
using PhotoMap.Api.Database.Entities;

namespace PhotoMap.Api.Database
{
    public class PhotoMapContext : DbContext
    {
        private static readonly ConcurrentDictionary<string, NpgsqlDataSource> DataSources = new();

        private readonly IConfiguration? _configuration;

        public DbSet<UserEntity> Users { get; set; } = null!;
        public DbSet<PhotoEntity> Photos { get; set; } = null!;
        public DbSet<PhotoSourceEntity> PhotoSources { get; set; } = null!;
        public DbSet<UserPhotoSourceStatusEntity> UserPhotoSourcesStatuses { get; set; } = null!;
        public DbSet<UserPhotoSourceEntity> UserPhotoSources { get; set; } = null!;
        public DbSet<FailedFileEntity> FailedFiles { get; set; } = null!;

        public PhotoMapContext(IConfiguration? configuration, DbContextOptions<PhotoMapContext> options)
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
            modelBuilder.ApplyConfiguration(new FailedFileEntityConfiguration());
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (_configuration == null || optionsBuilder.IsConfigured)
            {
                return;
            }

            var connectionString = _configuration["ConnectionString"]
                                   ?? throw new InvalidOperationException("Configuration property ConnectionString not specified.");

            optionsBuilder
                .UseNpgsql(GetDataSource(connectionString))
                .UseSnakeCaseNamingConvention();
        }

        private static NpgsqlDataSource GetDataSource(string connectionString)
        {
            return DataSources.GetOrAdd(connectionString, a =>
            {
                var dataSourceBuilder = new NpgsqlDataSourceBuilder(a);

                // entities keep objects (e.g. ClientAuthSettings) in jsonb columns
                dataSourceBuilder.EnableDynamicJson();

                return dataSourceBuilder.Build();
            });
        }
    }
}
