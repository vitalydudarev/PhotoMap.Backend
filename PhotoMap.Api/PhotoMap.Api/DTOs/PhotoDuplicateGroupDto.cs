namespace PhotoMap.Api.DTOs
{
    /// <summary>
    /// Photos of a user that are copies of one another: of the same contents, to the byte.
    /// </summary>
    public class PhotoDuplicateGroupDto
    {
        /// <summary>
        /// The ID of the first photo of the group.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// The photos of the group, by their ID, two at least.
        /// </summary>
        public PhotoDto[] Photos { get; set; } = null!;
    }
}
