namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// A tag of the photos of a user, which the gallery filters the photos by.
/// </summary>
/// <param name="Id">The ID of the tag.</param>
/// <param name="Name">The name of the tag, as the user first wrote it.</param>
/// <param name="PhotoCount">How many photos have the tag, the deleted ones too.</param>
public record PhotoTag(long Id, string Name, int PhotoCount);
