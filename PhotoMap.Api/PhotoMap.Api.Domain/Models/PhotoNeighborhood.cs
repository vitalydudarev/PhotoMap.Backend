using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// The photos taken around the time or the place a photo was, to suggest tags for it by: the photos taken between
/// <paramref name="From"/> and <paramref name="To"/>, and those taken within the box of coordinates, if any.
/// </summary>
public record PhotoNeighborhood(
    DateTimeOffset From,
    DateTimeOffset To,
    GeoBox? Box);

/// <summary>
/// A box of coordinates, in degrees.
/// </summary>
public record GeoBox(double MinLatitude, double MaxLatitude, double MinLongitude, double MaxLongitude)
{
    /// <summary>
    /// Reads a box written as <c>west,south,east,north</c>, the order of a GeoJSON bounding box, such as
    /// <c>-9.23,38.69,-9.09,38.8</c>: the longitudes from -180 to 180, the latitudes from -90 to 90, the west and the
    /// south sides no further than the east and the north ones.
    /// </summary>
    /// <returns>Whether the text is such a box.</returns>
    public static bool TryParse(string? text, [NotNullWhen(true)] out GeoBox? box)
    {
        box = null;

        var parts = text?.Split(',');

        if (parts is not { Length: 4 })
        {
            return false;
        }

        var values = new double[4];

        for (var i = 0; i < 4; i++)
        {
            if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]) ||
                !double.IsFinite(values[i]))
            {
                return false;
            }
        }

        var (west, south, east, north) = (values[0], values[1], values[2], values[3]);

        if (west < -180 || east > 180 || west > east || south < -90 || north > 90 || south > north)
        {
            return false;
        }

        box = new GeoBox(south, north, west, east);

        return true;
    }
}
