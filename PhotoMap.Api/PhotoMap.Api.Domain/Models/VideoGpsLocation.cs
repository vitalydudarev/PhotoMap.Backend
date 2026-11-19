namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// Where a video was taken, its GPS location.
/// </summary>
public record VideoGpsLocation(long VideoId, double Latitude, double Longitude);
