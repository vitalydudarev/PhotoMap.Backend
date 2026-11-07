namespace PhotoMap.Api.DTOs
{
    /// <summary>
    /// Tags to add to several photos of a user at once.
    /// </summary>
    public class AddPhotoTagsDto
    {
        /// <summary>
        /// The IDs of the photos.
        /// </summary>
        public long[] PhotoIds { get; set; } = [];

        /// <summary>
        /// The names of the tags. A name the user has a tag of, whatever its case, takes that tag; the other names
        /// make new tags.
        /// </summary>
        public string[] Tags { get; set; } = [];
    }
}
