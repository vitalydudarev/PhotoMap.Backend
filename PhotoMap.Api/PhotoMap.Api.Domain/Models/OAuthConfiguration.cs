namespace PhotoMap.Api.Domain.Models;

public class OAuthConfiguration
{
  public required string ClientId { get; set; }
  public required string RedirectUri { get; set; }
  public required string ResponseType { get; set; }
  public required string AuthorizeUrl { get; set; }
  public string? TokenUrl { get; set; }
  public string? Scope { get; set; }
}