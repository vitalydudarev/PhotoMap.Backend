namespace PhotoMap.Api.DTOs
{
    /// <summary>
    /// A location the videos of a user were taken in, as the reverse geocoding tells.
    /// </summary>
    public class VideoLocationDto
    {
        public long Id { get; set; }

        /// <summary>
        /// How large the location is: <c>Country</c>, <c>Region</c>, <c>City</c>, <c>Locality</c> or
        /// <c>Postcode</c>.
        /// </summary>
        public string Level { get; set; } = null!;

        /// <summary>
        /// The name of the location, such as "Minsk".
        /// </summary>
        public string Name { get; set; } = null!;

        /// <summary>
        /// The larger locations it is in, such as "Minsk Region, Belarus"; null for a country.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// How many videos of the user were taken in it.
        /// </summary>
        public int VideoCount { get; set; }

        /// <summary>
        /// The IDs of the larger locations of the user it is in, such as the region and the country of a city; none
        /// for a country.
        /// </summary>
        public long[] ParentIds { get; set; } = [];
    }
}
