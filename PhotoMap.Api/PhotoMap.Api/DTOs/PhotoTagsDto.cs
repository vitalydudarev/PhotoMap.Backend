namespace PhotoMap.Api.DTOs
{
    /// <summary>
    /// The tags of a photo.
    /// </summary>
    public class PhotoTagsDto
    {
        public long PhotoId { get; set; }

        /// <summary>
        /// The names of the tags of the photo, by name.
        /// </summary>
        public string[] Tags { get; set; } = [];
    }
}
