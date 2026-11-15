namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// Where a photo was taken, its GPS location.
/// </summary>
public record PhotoLocation(long PhotoId, double Latitude, double Longitude);
