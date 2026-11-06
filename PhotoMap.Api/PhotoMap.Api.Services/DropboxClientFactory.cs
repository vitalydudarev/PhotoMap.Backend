using Dropbox.Api;
using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Services;

public static class DropboxClientFactory
{
    /// <summary>
    /// Creates a client that refreshes the access token by itself when the user has granted offline access.
    /// </summary>
    /// <param name="appKey">The Dropbox app key (OAuth client ID) the tokens were issued to.</param>
    /// <param name="maxRetriesOnError">How many times the client makes a call again when Dropbox fails it with a
    /// server error, as the Dropbox SDK does by default when not given.</param>
    public static DropboxClient Create(UserAuthResult authResult, string appKey, HttpClient httpClient, int? maxRetriesOnError = null)
    {
        var config = new DropboxClientConfig("PhotoMap") { HttpClient = httpClient };
        if (maxRetriesOnError.HasValue)
        {
            config.MaxRetriesOnError = maxRetriesOnError.Value;
        }

        return authResult.RefreshToken == null
            ? new DropboxClient(authResult.Token, config)
            : new DropboxClient(authResult.Token, authResult.RefreshToken, authResult.TokenExpiresOn.UtcDateTime, appKey, config);
    }
}
