namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// An option of a filter and how many photos or videos it takes along with the other filters.
/// </summary>
public record FacetCount<T>(T Value, int Count);
