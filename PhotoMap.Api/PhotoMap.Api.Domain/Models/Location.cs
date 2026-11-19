namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// A location the photos of a user were taken in, which the gallery filters the photos by.
/// </summary>
/// <param name="Id">The ID of the location.</param>
/// <param name="Level">How large the location is.</param>
/// <param name="Name">The name of the location, such as "Minsk".</param>
/// <param name="Description">The larger locations it is in, such as "Minsk Region, Belarus"; null for a
/// country.</param>
/// <param name="PhotoCount">How many photos of the user were taken in it, the deleted ones too.</param>
public record Location(long Id, LocationLevel Level, string Name, string? Description, int PhotoCount);
