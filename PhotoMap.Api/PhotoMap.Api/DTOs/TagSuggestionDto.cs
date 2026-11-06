namespace PhotoMap.Api.DTOs
{
    /// <summary>
    /// A tag suggested for a photo, and why.
    /// </summary>
    public class TagSuggestionDto
    {
        public string Name { get; set; } = null!;

        /// <summary>
        /// Photos taken around the same time have the tag, or the tag tells when the photo was taken.
        /// </summary>
        public bool ByDate { get; set; }

        /// <summary>
        /// Photos taken nearby have the tag.
        /// </summary>
        public bool ByLocation { get; set; }
    }
}
