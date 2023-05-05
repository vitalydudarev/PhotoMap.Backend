namespace PhotoMap.Api.Services;

public class OAuthSettings
{
  public required string ClientId { get; set; }
  public required string RedirectUri { get; set; }
  public required string ResponseType { get; set; }
  public required string AuthorizeUrl { get; set; }
  public string? TokenUrl { get; set; }
    /*
     * oAuth: {
    yandexDisk: {
      clientId: '66de926ff5be4d2da65e5eb64435687b',
      redirectUri: 'http://localhost:4200/yandex-disk',
      responseType: 'token',
      authorizeUrl: 'https://oauth.yandex.ru/authorize'
    },
    dropbox: {
      clientId: '8pakfnac86x0iad',
      redirectUri: 'http://localhost:4200/dropbox',
      responseType: 'code',
      authorizeUrl: 'https://www.dropbox.com/oauth2/authorize',
      tokenUrl: 'https://www.dropbox.com/oauth2/token'
    }
  }
     */
}