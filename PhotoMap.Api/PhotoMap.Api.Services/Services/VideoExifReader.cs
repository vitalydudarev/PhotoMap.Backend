using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MetadataExtractor;
using MetadataExtractor.Formats.QuickTime;
using PhotoMap.Api.Domain.Models;
using Directory = MetadataExtractor.Directory;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// Reads the metadata of a video file with MetadataExtractor, as the worker reads the EXIF of the photos: the
/// QuickTime metadata of .mp4 and .mov files (the movie and track headers, and the metadata the camera has written,
/// such as its make and model, the GPS location and when the video was taken), and the little there is in .avi files.
/// </summary>
public static partial class VideoExifReader
{
    /// <summary>
    /// Dates before it are not dates the video was taken: a header that has none holds 0, which is 1904 in QuickTime.
    /// </summary>
    /// <summary>
    /// The text is kept as it is, such as the plus signs of a location and the names of places, rather than escaped:
    /// it is saved, not put in HTML.
    /// </summary>
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly DateTime EarliestDateTaken = new(1970, 1, 2, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Reads the metadata of the file, which the stream is open at the start of.
    /// </summary>
    /// <returns>The metadata, <see cref="VideoExif.Empty"/> for a file of a format that has none, or none that can
    /// be read.</returns>
    /// <exception cref="IOException">The file cannot be read, the metadata may be there.</exception>
    public static VideoExif Read(Stream stream)
    {
        IReadOnlyList<Directory> directories;
        try
        {
            directories = ImageMetadataReader.ReadMetadata(stream);
        }
        catch (ImageProcessingException)
        {
            // not a format MetadataExtractor knows, it would not know it on the next run either
            return VideoExif.Empty;
        }

        return FromDirectories(directories);
    }

    public static VideoExif FromDirectories(IReadOnlyList<Directory> directories)
    {
        var (latitude, longitude) = GetLocation(directories);

        return new VideoExif(ToJson(directories), GetDateTimeTaken(directories), latitude, longitude);
    }

    /// <summary>
    /// Every directory, as an object of its tags by their names. A file has a track header for each of its tracks,
    /// their directories are numbered after the first.
    /// </summary>
    private static string ToJson(IReadOnlyList<Directory> directories)
    {
        var json = new JsonObject();

        foreach (var directory in directories)
        {
            if (directory.TagCount == 0)
            {
                continue;
            }

            var tags = new JsonObject();
            foreach (var tag in directory.Tags)
            {
                tags[tag.Name] = ToJsonValue(directory, tag);
            }

            var name = directory.Name;
            for (var number = 2; json.ContainsKey(name); number++)
            {
                name = $"{directory.Name} {number}";
            }

            json[name] = tags;
        }

        return json.ToJsonString(JsonSerializerOptions);
    }

    /// <summary>
    /// The text of a tag without the null some cameras end it with.
    /// </summary>
    private static string TrimEnd(string text)
    {
        return text.TrimEnd('\0', ' ');
    }

    /// <summary>
    /// The value of the tag as a number, a date or a string, rather than its description, which is for display
    /// and does not keep the time zone of a date. What is neither, such as a matrix, is given as its description.
    /// </summary>
    private static JsonNode? ToJsonValue(Directory directory, Tag tag)
    {
        var value = directory.GetObject(tag.Type);

        return value switch
        {
            null => null,
            string text => TrimEnd(text),
            StringValue text => TrimEnd(text.ToString()),
            byte or sbyte or short or ushort or int or uint or long or ulong => JsonValue.Create(Convert.ToInt64(value)),
            float or double => Convert.ToDouble(value) is var number && double.IsFinite(number) ? JsonValue.Create(number) : null,
            // the fixed-point numbers of QuickTime, such as the width and height of a track
            decimal number => JsonValue.Create(number),
            Rational rational => rational.Denominator == 0 ? null : JsonValue.Create(rational.ToDouble()),
            DateTime dateTime => ToUtc(dateTime).ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
            TimeSpan timeSpan => JsonValue.Create(timeSpan.TotalSeconds),
            // a list of brands of a file type may end with empty ones, which are nulls
            _ => tag.Description?.Replace("\0", "").TrimEnd(',', ' ')
        };
    }

    /// <summary>
    /// When the video was taken: as the camera has written it, with its time zone, or else when the movie was
    /// created, which QuickTime keeps in UTC.
    /// </summary>
    private static DateTimeOffset? GetDateTimeTaken(IReadOnlyList<Directory> directories)
    {
        var metadataHeader = directories.OfType<QuickTimeMetadataHeaderDirectory>().FirstOrDefault();
        if (metadataHeader?.GetObject(QuickTimeMetadataHeaderDirectory.TagCreationDate) is DateTime creationDate &&
            IsDateTaken(creationDate))
        {
            return new DateTimeOffset(ToUtc(creationDate));
        }

        var movieHeader = directories.OfType<QuickTimeMovieHeaderDirectory>().FirstOrDefault();
        if (movieHeader?.GetObject(QuickTimeMovieHeaderDirectory.TagCreated) is DateTime created && IsDateTaken(created))
        {
            return new DateTimeOffset(ToUtc(created));
        }

        return null;
    }

    private static bool IsDateTaken(DateTime dateTime)
    {
        var utc = ToUtc(dateTime);

        return utc >= EarliestDateTaken && utc <= DateTime.UtcNow.AddDays(1);
    }

    /// <summary>
    /// The dates with a time zone are made local times by MetadataExtractor, those without one are QuickTime times,
    /// which are UTC.
    /// </summary>
    private static DateTime ToUtc(DateTime dateTime)
    {
        return dateTime.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
            : dateTime.ToUniversalTime();
    }

    private static (double? Latitude, double? Longitude) GetLocation(IReadOnlyList<Directory> directories)
    {
        var location = directories.OfType<QuickTimeMetadataHeaderDirectory>()
            .Select(a => a.GetString(QuickTimeMetadataHeaderDirectory.TagGpsLocation))
            .FirstOrDefault(a => !string.IsNullOrEmpty(a));

        return ParseIso6709(location);
    }

    /// <summary>
    /// The latitude and longitude of a location of ISO 6709 in degrees, as phones write it: <c>+53.9095+027.5202+224.391/</c>,
    /// the altitude and the slash optional. Locations in degrees and minutes are not read, nor is 0, 0, which is
    /// a location that has not been found.
    /// </summary>
    public static (double? Latitude, double? Longitude) ParseIso6709(string? location)
    {
        if (location == null)
        {
            return (null, null);
        }

        var match = Iso6709Regex().Match(TrimEnd(location.Trim()));
        if (!match.Success)
        {
            return (null, null);
        }

        var latitude = double.Parse(match.Groups["latitude"].Value, CultureInfo.InvariantCulture);
        var longitude = double.Parse(match.Groups["longitude"].Value, CultureInfo.InvariantCulture);

        if (Math.Abs(latitude) > 90 || Math.Abs(longitude) > 180 || (latitude == 0 && longitude == 0))
        {
            return (null, null);
        }

        return (latitude, longitude);
    }

    [GeneratedRegex(@"^(?<latitude>[+-]\d{1,2}(\.\d+)?)(?<longitude>[+-]\d{1,3}(\.\d+)?)([+-]\d+(\.\d+)?)?(CRS[^/]*)?/?$")]
    private static partial Regex Iso6709Regex();
}
