namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// How large a location of the reverse geocoding is, from a whole country down to a postcode. Saved as a number.
/// </summary>
public enum LocationLevel
{
    Country = 1,
    Region = 2,

    /// <summary>
    /// A city or a town, the "place" of the reverse geocoding.
    /// </summary>
    City = 3,

    /// <summary>
    /// A part of a city, such as a district.
    /// </summary>
    Locality = 4,
    Postcode = 5
}
