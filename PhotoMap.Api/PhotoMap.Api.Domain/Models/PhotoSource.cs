namespace PhotoMap.Api.Domain.Models;

public class PhotoSource
{
    public required long Id { get; set; }
    public required string Name { get; set; }
    public required string ServiceSettings { get; set; }
    public required ClientAuthSettings ClientAuthSettings { get; set; }
    public required string ServiceFactoryImplementationType { get; set; }
}