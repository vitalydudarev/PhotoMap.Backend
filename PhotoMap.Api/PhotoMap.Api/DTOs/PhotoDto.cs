using System;

namespace PhotoMap.Api.DTOs
{
    public class PhotoDto
    {
        public long Id { get; set; }
        public string PhotoUrl { get; set; } = null!;
        public string ThumbnailSmallUrl { get; set; } = null!;
        public string ThumbnailLargeUrl { get; set; } = null!;
        public DateTime DateTimeTaken { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        /// <summary>
        /// How far off the location may be, in meters, as the camera reported it; null when it did not.
        /// </summary>
        public double? HorizontalPositioningError { get; set; }
        public string FileName { get; set; } = null!;

        /// <summary>
        /// The path of the photo in its photo source, with its file name.
        /// </summary>
        public string? Path { get; set; }

        /// <summary>
        /// When the user marked the photo as deleted, null while it is not.
        /// </summary>
        public DateTime? DeletedOn { get; set; }

        /// <summary>
        /// The names of the tags of the photo, by name. Only the photos of the gallery tell them.
        /// </summary>
        public string[] Tags { get; set; } = [];
    }
}
