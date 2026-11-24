namespace PhotoMap.Api.DTOs
{
    /// <summary>
    /// The tags of a video.
    /// </summary>
    public class VideoTagsDto
    {
        public long VideoId { get; set; }

        /// <summary>
        /// The names of the tags of the video, by name.
        /// </summary>
        public string[] Tags { get; set; } = [];
    }
}
