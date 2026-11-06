namespace PhotoMap.Api.DTOs
{
    /// <summary>
    /// A tag of the photos of a user.
    /// </summary>
    public class PhotoTagDto
    {
        public long Id { get; set; }

        /// <summary>
        /// The name of the tag, as the user first wrote it.
        /// </summary>
        public string Name { get; set; } = null!;

        /// <summary>
        /// How many photos have the tag, the deleted ones too.
        /// </summary>
        public int PhotoCount { get; set; }
    }
}
