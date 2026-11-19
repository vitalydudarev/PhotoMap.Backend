using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Database.Entities;

/// <summary>
/// A location photos or videos were taken in, as the reverse geocoding tells, see <see cref="GeocodedLocation"/>. Of
/// every user: a location is saved once, when the first photo or video taken in it is geocoded.
/// </summary>
public class LocationEntity
{
    public long Id { get; set; }
    public LocationLevel Level { get; set; }

    /// <summary>
    /// What tells the location apart from the others of its level, see <see cref="GeocodedLocation.Key"/>.
    /// </summary>
    public required string Key { get; set; }
    public required string Name { get; set; }

    /// <summary>
    /// The larger locations it is in, such as "Minsk Region, Belarus"; null for a country.
    /// </summary>
    public string? Description { get; set; }

    public List<PhotoLocationEntity> Photos { get; set; } = [];
    public List<VideoLocationEntity> Videos { get; set; } = [];
}
