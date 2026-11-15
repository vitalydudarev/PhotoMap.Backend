namespace PhotoMap.Api.DTOs;

/// <param name="Address">The address of the GPS location of the photo, null when it has none (yet).</param>
public record PhotoAddressDto(string? Address);
