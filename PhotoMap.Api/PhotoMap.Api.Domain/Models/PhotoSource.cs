namespace PhotoMap.Api.Domain.Models;

public class PhotoSource
{
    public required long Id { get; set; }
    public required string Name { get; set; }
    public required string Settings { get; set; }
    public required string ImplementationType { get; set; }
}