using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PhotoMap.Api.Database
{
    /// <summary>
    /// Used by the EF Core tools (e.g. dotnet ef migrations add) to create the context.
    /// </summary>
    public class PhotoMapContextDesignTimeFactory : IDesignTimeDbContextFactory<PhotoMapContext>
    {
        public PhotoMapContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<PhotoMapContext>()
                .UseNpgsql("Server=localhost;Port=5432;User Id=postgres;Password=postgres;Database=photo-map-db")
                .UseSnakeCaseNamingConvention()
                .Options;

            return new PhotoMapContext(null, options);
        }
    }
}
