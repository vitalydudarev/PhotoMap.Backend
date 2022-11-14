namespace PhotoMap.Api.Domain.Models;

public class Photo
{
    public int Id { get; set; }

    public string PhotoUrl { get; set; }

    public string ThumbnailSmallUrl { get; set; }

    public string ThumbnailLargeUrl { get; set; }

    public DateTime DateTimeTaken { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public string FileName { get; set; }
}