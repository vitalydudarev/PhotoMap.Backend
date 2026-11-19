using System.Text.Json;
using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// The locations a photo was taken in, its country, region, city, locality and postcode, read from the response of
/// the Mapbox reverse geocoding saved for it.
/// </summary>
public static class GeocodedLocations
{
    /// <summary>
    /// The names Mapbox gives the levels in the context of a feature.
    /// </summary>
    private static readonly (string Name, LocationLevel Level)[] ContextLevels =
    [
        ("country", LocationLevel.Country),
        ("region", LocationLevel.Region),
        ("place", LocationLevel.City),
        ("locality", LocationLevel.Locality),
        ("postcode", LocationLevel.Postcode)
    ];

    /// <summary>
    /// The locations of the contexts of the features. The context of a feature has the feature itself and the
    /// larger ones it is in; of a level, the location of the first feature that has one is taken.
    ///
    /// A location is told apart by its Mapbox ID, but a postcode by its country and its name: Mapbox gives a postcode
    /// different IDs, and the same ID to different postcodes.
    /// </summary>
    /// <param name="geocoding">The response of the Mapbox reverse geocoding, a GeoJSON FeatureCollection.</param>
    /// <returns>The locations, from the country down; none when the photo has not been geocoded, nothing was found at
    /// its location, or the response cannot be read.</returns>
    public static IReadOnlyList<GeocodedLocation> Get(string? geocoding)
    {
        if (string.IsNullOrEmpty(geocoding))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(geocoding);

            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("features", out var features) ||
                features.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var contexts = features.EnumerateArray()
                .Select(GetContext)
                .Where(a => a.HasValue)
                .Select(a => a!.Value)
                .ToList();

            var entries = new Dictionary<LocationLevel, JsonElement>();

            foreach (var (name, level) in ContextLevels)
            {
                foreach (var context in contexts)
                {
                    if (context.TryGetProperty(name, out var entry) && entry.ValueKind == JsonValueKind.Object &&
                        !string.IsNullOrWhiteSpace(GetString(entry, "name")))
                    {
                        entries[level] = entry;
                        break;
                    }
                }
            }

            return GetLocations(entries);
        }
        catch (JsonException)
        {
            // a response that cannot be read tells no location
            return [];
        }
    }

    private static List<GeocodedLocation> GetLocations(Dictionary<LocationLevel, JsonElement> entries)
    {
        string? NameOf(LocationLevel level) =>
            entries.TryGetValue(level, out var entry) ? GetString(entry, "name")!.Trim() : null;

        var country = NameOf(LocationLevel.Country);
        var region = NameOf(LocationLevel.Region);
        var city = NameOf(LocationLevel.City);
        var countryCode = entries.TryGetValue(LocationLevel.Country, out var countryEntry)
            ? GetString(countryEntry, "country_code")?.Trim().ToUpperInvariant()
            : null;

        var locations = new List<GeocodedLocation>();

        foreach (var (_, level) in ContextLevels)
        {
            if (!entries.TryGetValue(level, out var entry))
            {
                continue;
            }

            var name = NameOf(level)!;

            // a city is often a region of its own, of the same name, which tells nothing more
            var description = level switch
            {
                LocationLevel.Country => null,
                LocationLevel.Region => Join(country),
                LocationLevel.City => Join(SameName(region, name) ? null : region, country),
                _ => Join(SameName(city, name) ? null : city, country)
            };

            var key = level == LocationLevel.Postcode
                ? $"{countryCode}:{name}"
                : GetString(entry, "mapbox_id") ?? $"{name}, {description}";

            locations.Add(new GeocodedLocation(level, key, name, description));
        }

        return locations;
    }

    private static JsonElement? GetContext(JsonElement feature)
    {
        return feature.ValueKind == JsonValueKind.Object &&
               feature.TryGetProperty("properties", out var properties) &&
               properties.ValueKind == JsonValueKind.Object &&
               properties.TryGetProperty("context", out var context) &&
               context.ValueKind == JsonValueKind.Object
            ? context
            : null;
    }

    private static bool SameName(string? a, string b)
    {
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private static string? Join(params string?[] names)
    {
        var parts = names.Where(a => !string.IsNullOrWhiteSpace(a)).ToList();

        return parts.Count > 0 ? string.Join(", ", parts) : null;
    }

    private static string? GetString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
