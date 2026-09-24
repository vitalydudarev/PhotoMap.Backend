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
        public string FileName { get; set; } = null!;
    }
}
