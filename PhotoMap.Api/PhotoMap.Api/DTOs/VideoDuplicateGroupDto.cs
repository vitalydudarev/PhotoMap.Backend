namespace PhotoMap.Api.DTOs
{
    /// <summary>
    /// Videos of a user that are copies of one another: of the same size and file name.
    /// </summary>
    public class VideoDuplicateGroupDto
    {
        /// <summary>
        /// The ID of the first video of the group.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// The file name of the first video of the group, the others differ from it in case at most.
        /// </summary>
        public string FileName { get; set; } = null!;

        /// <summary>
        /// The size of each video of the group, in bytes.
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// The videos of the group, by their ID, two at least.
        /// </summary>
        public VideoDto[] Videos { get; set; } = null!;
    }
}
