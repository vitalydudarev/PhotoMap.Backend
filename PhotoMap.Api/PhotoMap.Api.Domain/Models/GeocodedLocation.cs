namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// A location a photo was taken in, as the reverse geocoding of the photo tells: its country, its city and so on.
/// </summary>
/// <param name="Level">How large the location is.</param>
/// <param name="Key">What tells the location apart from the others of its level, the same for every photo taken in
/// it.</param>
/// <param name="Name">The name of the location, such as "Minsk".</param>
/// <param name="Description">The larger locations it is in, such as "Minsk Region, Belarus", which tell apart the
/// locations of the same name; null for a country.</param>
public record GeocodedLocation(LocationLevel Level, string Key, string Name, string? Description);
