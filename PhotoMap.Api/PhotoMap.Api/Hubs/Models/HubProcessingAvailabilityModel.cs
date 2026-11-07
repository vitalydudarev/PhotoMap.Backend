namespace PhotoMap.Api.Hubs.Models;

/// <param name="Available">Whether photos and videos can be processed.</param>
/// <param name="Reason">Why they cannot, null while they can.</param>
public record HubProcessingAvailabilityModel(bool Available, string? Reason);
