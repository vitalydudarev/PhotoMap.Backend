namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// A location the photos, or the videos, of a user were taken in, which the gallery filters them by.
/// </summary>
/// <param name="Id">The ID of the location.</param>
/// <param name="Level">How large the location is.</param>
/// <param name="Name">The name of the location, such as "Minsk".</param>
/// <param name="Description">The larger locations it is in, such as "Minsk Region, Belarus"; null for a
/// country.</param>
/// <param name="Count">How many photos, or videos, of the user were taken in it, the deleted photos too.</param>
/// <param name="ParentIds">The IDs of the larger locations of the user it is in, such as the region and the country
/// of a city: those of a larger level that its photos, or videos, were taken in too; none for a country.</param>
public record Location(long Id, LocationLevel Level, string Name, string? Description, int Count,
    IReadOnlyList<long> ParentIds);
