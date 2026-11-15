using System.Text.Json;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// The address of a photo, read from the response of the Mapbox reverse geocoding saved for it.
/// </summary>
public static class GeocodedAddress
{
    private const string AddressFeatureType = "address";

    /// <summary>
    /// The address of the feature of the type "address", or of the first feature when there is none of that type: its
    /// full address, otherwise its name and the place it is in.
    /// </summary>
    /// <param name="geocoding">The response of the Mapbox reverse geocoding, a GeoJSON FeatureCollection.</param>
    /// <returns>The address, or null when the photo has not been geocoded, nothing was found at its location, or the
    /// response cannot be read.</returns>
    public static string? Get(string? geocoding)
    {
        if (string.IsNullOrEmpty(geocoding))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(geocoding);

            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("features", out var features) ||
                features.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var properties = features.EnumerateArray()
                .Select(GetProperties)
                .Where(a => a.HasValue)
                .Select(a => a!.Value)
                .ToList();

            var feature = properties.FirstOrDefault(a => GetString(a, "feature_type") == AddressFeatureType);
            if (feature.ValueKind == JsonValueKind.Undefined)
            {
                feature = properties.FirstOrDefault();
            }

            return feature.ValueKind == JsonValueKind.Undefined ? null : GetAddress(feature);
        }
        catch (JsonException)
        {
            // a response that cannot be read tells no address
            return null;
        }
    }

    private static JsonElement? GetProperties(JsonElement feature)
    {
        return feature.ValueKind == JsonValueKind.Object &&
               feature.TryGetProperty("properties", out var properties) &&
               properties.ValueKind == JsonValueKind.Object
            ? properties
            : null;
    }

    private static string? GetAddress(JsonElement properties)
    {
        var fullAddress = GetString(properties, "full_address");
        if (!string.IsNullOrWhiteSpace(fullAddress))
        {
            return fullAddress;
        }

        var parts = new[] { GetString(properties, "name"), GetString(properties, "place_formatted") }
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .ToList();

        return parts.Count > 0 ? string.Join(", ", parts) : null;
    }

    private static string? GetString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
