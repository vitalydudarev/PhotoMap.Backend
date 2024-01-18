using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PhotoMap.Api.Database;

namespace PhotoMap.Api.Services.Tests;

public class UserPhotoSourceServiceTests
{
    [Fact]
    public async Task Test()
    {
        var configurationManager = new ConfigurationManager();
        configurationManager["ConnectionString"] =
            "Server=localhost;Port=5432;User Id=postgres;Password=postgres;Database=photo-map-db;Integrated Security=true;Pooling=true;";

        var context = new PhotoMapContext(configurationManager, new DbContextOptions<PhotoMapContext>());
        // var userPhotoSourceEntities = await context.UserPhotoSourcesAuth.Where(a => a.UserId == 1)
            // .Include(b => b.PhotoSource).ToListAsync();

            // var userPhotoSourceEntities = await context.UserPhotoSources.Where(a => a.UserId == 1)
                // .Include(b => b.PhotoSource).Include(userPhotoSourceEntity => userPhotoSourceEntity.UserAuthSettings)
                // .ToListAsync();
    }
}