namespace PhotoMap.Api.Domain.Models;

public class OAuthSettings
{
  public required string ClientId { get; set; }
  public required string RedirectUri { get; set; }
  public required string ResponseType { get; set; }
  public required string AuthorizeUrl { get; set; }
  public string? TokenUrl { get; set; }
}