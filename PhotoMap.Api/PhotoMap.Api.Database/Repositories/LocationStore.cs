using Microsoft.EntityFrameworkCore;
using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Database.Repositories;

/// <summary>
/// Saves the locations photos and videos were taken in, the ones shared by every user, see
/// <see cref="Entities.LocationEntity"/>.
/// </summary>
public static class LocationStore
{
    /// <summary>
    /// Saves the locations not saved yet, within the transaction of the caller; one saved meanwhile, by another photo
    /// or video, is taken as it is.
    /// </summary>
    /// <returns>The IDs of the locations, by their levels and keys.</returns>
    public static async Task<IReadOnlyDictionary<(LocationLevel Level, string Key), long>> SaveAsync(
        PhotoMapContext context, IEnumerable<GeocodedLocation> geocodedLocations)
    {
        var locations = geocodedLocations.DistinctBy(a => (a.Level, a.Key)).ToList();

        if (locations.Count == 0)
        {
            return new Dictionary<(LocationLevel, string), long>();
        }

        var levels = locations.Select(a => (int)a.Level).ToArray();
        var keys = locations.Select(a => a.Key).ToArray();
        var names = locations.Select(a => a.Name).ToArray();
        var descriptions = locations.Select(a => a.Description).ToArray();

        // EF Core has no upsert; ON CONFLICT keeps a location saved meanwhile as it is
        await context.Database.ExecuteSqlAsync($"""
            INSERT INTO locations (level, key, name, description)
            SELECT * FROM unnest({levels}, {keys}, {names}, {descriptions})
            ON CONFLICT (level, key) DO NOTHING
            """);

        var distinctKeys = keys.Distinct().ToArray();
        var saved = await context.Locations
            .AsNoTracking()
            .Where(a => distinctKeys.Contains(a.Key))
            .Select(a => new { a.Id, a.Level, a.Key })
            .ToListAsync();

        // a key of another level, such as a city that is a region too, is left out
        var wanted = locations.Select(a => (a.Level, a.Key)).ToHashSet();

        return saved
            .Where(a => wanted.Contains((a.Level, a.Key)))
            .ToDictionary(a => (a.Level, a.Key), a => a.Id);
    }

    /// <summary>
    /// The locations of the IDs given, by level, from the countries down, and by name, with how many photos or videos
    /// of a user were taken in each, and the larger locations each is in.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="countsByLocationId">How many photos or videos of the user were taken in each location.</param>
    /// <param name="parents">Each location, with a larger one a photo or video of the user taken in it was taken in
    /// too; as a photo or video is geocoded all at once, the one is in the other.</param>
    public static async Task<IReadOnlyList<Location>> GetAsync(PhotoMapContext context,
        IReadOnlyDictionary<long, int> countsByLocationId, IEnumerable<LocationParent> parents)
    {
        var parentIdsByLocationId = parents
            .GroupBy(a => a.LocationId)
            .ToDictionary(a => a.Key, a => (IReadOnlyList<long>)a.Select(b => b.ParentId).Distinct().Order().ToList());

        var locationIds = countsByLocationId.Keys.ToArray();

        var locations = await context.Locations
            .AsNoTracking()
            .Where(a => locationIds.Contains(a.Id))
            .ToListAsync();

        return locations
            .OrderBy(a => a.Level)
            .ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(a => a.Description, StringComparer.CurrentCultureIgnoreCase)
            .Select(a => new Location(a.Id, a.Level, a.Name, a.Description, countsByLocationId[a.Id],
                parentIdsByLocationId.GetValueOrDefault(a.Id, [])))
            .ToList();
    }
}

/// <summary>
/// A location and a larger one it is in, such as a city and its country.
/// </summary>
public record LocationParent(long LocationId, long ParentId);
