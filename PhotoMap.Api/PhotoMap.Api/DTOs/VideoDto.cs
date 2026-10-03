using System;

namespace PhotoMap.Api.DTOs
{
    public class VideoDto
    {
        public long Id { get; set; }
        public long PhotoSourceId { get; set; }
        public string PreviewUrl { get; set; } = null!;
        public string FileName { get; set; } = null!;
        public string? FolderPath { get; set; }
        public string? MimeType { get; set; }
        public long Size { get; set; }
        public DateTime DateTimeTaken { get; set; }
        public DateTime? ExifDateTime { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }
}
