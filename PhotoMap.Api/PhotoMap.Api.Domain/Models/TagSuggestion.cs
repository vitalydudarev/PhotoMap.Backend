namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// A tag suggested for a photo, and why.
/// </summary>
/// <param name="Name">The name of the tag.</param>
/// <param name="ByDate">Photos taken around the same time have the tag, or the tag tells when the photo was taken.</param>
/// <param name="ByLocation">Photos taken nearby have the tag.</param>
public record TagSuggestion(string Name, bool ByDate, bool ByLocation);
