using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// Suggests tags for a photo by when and where it was taken: the tags of the photos taken around the same time or
/// nearby, the nearer the better, and tags that tell the year and the season the photo was taken in.
/// </summary>
public static class PhotoTagSuggester
{
    /// <summary>
    /// How far apart in time the photos of the same trip or event may be taken.
    /// </summary>
    public static readonly TimeSpan MaxTimeApart = TimeSpan.FromDays(7);

    /// <summary>
    /// How far apart, in kilometers, the photos of the same place may be taken.
    /// </summary>
    public const double MaxKilometersApart = 5;

    /// <summary>
    /// The most tags suggested.
    /// </summary>
    public const int MaxSuggestions = 10;

    private const double EarthRadiusKilometers = 6371;
    private const double KilometersPerDegreeOfLatitude = Math.PI * EarthRadiusKilometers / 180;

    /// <returns>The time and the box of coordinates around the photo to look for the photos with tags in.</returns>
    public static PhotoNeighborhood GetNeighborhood(Photo photo)
    {
        GeoBox? box = null;

        if (photo is { HasGps: true, Latitude: { } latitude, Longitude: { } longitude })
        {
            var latitudeDelta = MaxKilometersApart / KilometersPerDegreeOfLatitude;
            // a degree of longitude shrinks towards the poles; near them, any longitude is near enough
            var cos = Math.Cos(latitude * Math.PI / 180);
            var longitudeDelta = cos > 0.01 ? latitudeDelta / cos : 180;

            box = new GeoBox(latitude - latitudeDelta, latitude + latitudeDelta,
                longitude - longitudeDelta, longitude + longitudeDelta);
        }

        return new PhotoNeighborhood(photo.DateTimeTaken - MaxTimeApart, photo.DateTimeTaken + MaxTimeApart, box);
    }

    /// <param name="photo">The photo to suggest tags for.</param>
    /// <param name="taggedPhotos">The photos with tags around the photo, which may be among them.</param>
    /// <returns>The tags suggested, the likeliest first. The ones the photo has are left out.</returns>
    public static IReadOnlyList<TagSuggestion> Suggest(Photo photo, IReadOnlyCollection<TaggedPhoto> taggedPhotos)
    {
        var own = taggedPhotos
            .Where(a => a.Id == photo.Id)
            .SelectMany(a => a.Tags)
            .Concat(photo.Tags)
            .Select(TagNames.Normalize)
            .ToHashSet();

        var scores = new Dictionary<string, Score>();

        foreach (var other in taggedPhotos.Where(a => a.Id != photo.Id))
        {
            var byDate = GetDateWeight(photo.DateTimeTaken, other.DateTimeTaken);
            var byLocation = GetLocationWeight(photo, other);

            if (byDate == 0 && byLocation == 0)
            {
                continue;
            }

            // a photo with the tag twice over, in a different case, counts it once
            foreach (var name in other.Tags.DistinctBy(TagNames.Normalize))
            {
                var key = TagNames.Normalize(name);
                if (own.Contains(key))
                {
                    continue;
                }

                var score = scores.TryGetValue(key, out var existing) ? existing : new Score(name);
                score.Value += byDate + byLocation;
                score.ByDate |= byDate > 0;
                score.ByLocation |= byLocation > 0;
                scores[key] = score;
            }
        }

        var suggestions = scores.Values
            .OrderByDescending(a => a.Value)
            .ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(a => new TagSuggestion(a.Name, a.ByDate, a.ByLocation))
            .Take(MaxSuggestions)
            .ToList();

        // a photo with no tags around it gets these at least, so the first tags are a click away too
        foreach (var name in GetDateTags(photo))
        {
            var key = TagNames.Normalize(name);
            if (suggestions.Count < MaxSuggestions && !own.Contains(key) &&
                suggestions.All(a => TagNames.Normalize(a.Name) != key))
            {
                suggestions.Add(new TagSuggestion(name, true, false));
            }
        }

        return suggestions;
    }

    /// <returns>The year and the season, in UTC, the photo was taken in, such as 2019 and Summer 2019. The winter of
    /// the north, and the summer of the south, run from one year into the next, such as Winter 2019–2020.</returns>
    public static IReadOnlyList<string> GetDateTags(Photo photo)
    {
        var taken = photo.DateTimeTaken.UtcDateTime;
        var southern = photo is { HasGps: true, Latitude: < 0 };

        // December starts the season of the turn of the year, which January and February end
        var (season, startYear) = taken.Month switch
        {
            12 => (0, taken.Year),
            1 or 2 => (0, taken.Year - 1),
            >= 3 and <= 5 => (1, taken.Year),
            >= 6 and <= 8 => (2, taken.Year),
            _ => (3, taken.Year)
        };

        string[] names = southern
            ? ["Summer", "Autumn", "Winter", "Spring"]
            : ["Winter", "Spring", "Summer", "Autumn"];

        var seasonTag = season == 0
            ? $"{names[season]} {startYear}–{startYear + 1}"
            : $"{names[season]} {startYear}";

        return [taken.Year.ToString(), seasonTag];
    }

    /// <returns>1 for photos taken at the same moment, down to none for those taken
    /// <see cref="MaxTimeApart"/> apart or more: a day apart weighs a half, two days a third.</returns>
    private static double GetDateWeight(DateTimeOffset taken, DateTimeOffset otherTaken)
    {
        var apart = (taken - otherTaken).Duration();

        return apart > MaxTimeApart ? 0 : 1 / (1 + apart.TotalDays);
    }

    /// <returns>1 for photos taken at the same place, down to none for those taken
    /// <see cref="MaxKilometersApart"/> apart or more, or of which either has no location: a kilometer apart
    /// weighs a half.</returns>
    private static double GetLocationWeight(Photo photo, TaggedPhoto other)
    {
        if (photo is not { HasGps: true, Latitude: { } latitude, Longitude: { } longitude } ||
            other is not { Latitude: { } otherLatitude, Longitude: { } otherLongitude })
        {
            return 0;
        }

        var kilometers = GetKilometersApart(latitude, longitude, otherLatitude, otherLongitude);

        return kilometers > MaxKilometersApart ? 0 : 1 / (1 + kilometers);
    }

    /// <summary>
    /// The haversine distance between two points.
    /// </summary>
    public static double GetKilometersApart(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        var dLatitude = (latitude2 - latitude1) * Math.PI / 180;
        var dLongitude = (longitude2 - longitude1) * Math.PI / 180;

        var a = Math.Sin(dLatitude / 2) * Math.Sin(dLatitude / 2) +
                Math.Cos(latitude1 * Math.PI / 180) * Math.Cos(latitude2 * Math.PI / 180) *
                Math.Sin(dLongitude / 2) * Math.Sin(dLongitude / 2);

        return 2 * EarthRadiusKilometers * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    private sealed class Score(string name)
    {
        public string Name { get; } = name;
        public double Value { get; set; }
        public bool ByDate { get; set; }
        public bool ByLocation { get; set; }
    }
}
