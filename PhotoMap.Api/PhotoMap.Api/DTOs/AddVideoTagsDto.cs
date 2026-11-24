namespace PhotoMap.Api.DTOs
{
    /// <summary>
    /// Tags to add to several videos of a user at once; the tags are those of the photos too.
    /// </summary>
    public class AddVideoTagsDto
    {
        /// <summary>
        /// The IDs of the videos.
        /// </summary>
        public long[] VideoIds { get; set; } = [];

        /// <summary>
        /// The names of the tags. A name the user has a tag of, whatever its case, takes that tag; the other names
        /// make new tags.
        /// </summary>
        public string[] Tags { get; set; } = [];
    }
}
