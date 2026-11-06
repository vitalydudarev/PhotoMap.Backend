using System.Net;
using System.Text;
using Dropbox.Api;
using Microsoft.Extensions.Logging.Abstractions;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Services.Tests;

public class DropboxSourceFilesTests
{
    private static readonly SourceFile File = new("a.jpg", "/Camera Uploads/a.jpg", 3, SourceDownloadContent.Photos);

    [Fact]
    public async Task CopyToAsync_ShouldCopyTheContentsOfTheFile()
    {
        // Arrange
        var handler = new FakeDropboxHandler(_ => Download([1, 2, 3]));
        using var sourceFiles = CreateSourceFiles(handler);
        using var destination = new MemoryStream();

        // Act
        await sourceFiles.CopyToAsync(File, destination, CancellationToken.None);

        // Assert
        Assert.Equal([1, 2, 3], destination.ToArray());
        Assert.Equal(1, handler.Downloads);
    }

    [Fact]
    public async Task CopyToAsync_ShouldRetryTwice_WhenTheDownloadKeepsFailing()
    {
        // Arrange
        var handler = new FakeDropboxHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var sourceFiles = CreateSourceFiles(handler);

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(() => sourceFiles.CopyToAsync(File, new MemoryStream(), CancellationToken.None));

        // the first attempt and 2 retries, the client does not retry by itself
        Assert.Equal(1 + DropboxSourceFiles.DownloadMaxRetries, handler.Downloads);
    }

    [Fact]
    public async Task CopyToAsync_ShouldDownloadTheFile_WhenARetrySucceeds()
    {
        // Arrange
        var handler = new FakeDropboxHandler(attempt => attempt == 1
            ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
            : Download([4, 5]));
        using var sourceFiles = CreateSourceFiles(handler);
        using var destination = new MemoryStream();

        // Act
        await sourceFiles.CopyToAsync(File, destination, CancellationToken.None);

        // Assert
        Assert.Equal([4, 5], destination.ToArray());
        Assert.Equal(2, handler.Downloads);
    }

    [Fact]
    public async Task CopyToAsync_ShouldNotRetry_WhenTheTokenIsNotAccepted()
    {
        // Arrange
        var handler = new FakeDropboxHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("""{"error_summary": "invalid_access_token/", "error": {".tag": "invalid_access_token"}}""",
                Encoding.UTF8, "application/json")
        });
        using var sourceFiles = CreateSourceFiles(handler);

        // Act & Assert
        await Assert.ThrowsAsync<AuthException>(() => sourceFiles.CopyToAsync(File, new MemoryStream(), CancellationToken.None));

        Assert.Equal(1, handler.Downloads);
    }

    private static DropboxSourceFiles CreateSourceFiles(FakeDropboxHandler handler)
    {
        DropboxClient CreateClient(int maxRetriesOnError) => new("token",
            new DropboxClientConfig("PhotoMap.Tests") { HttpClient = new HttpClient(handler), MaxRetriesOnError = maxRetriesOnError });

        return new DropboxSourceFiles(CreateClient(4), CreateClient(0), new DropboxSettings(), NullLogger.Instance,
            _ => TimeSpan.Zero);
    }

    /// <summary>
    /// The response of a download, with the metadata of the file Dropbox sends in a header.
    /// </summary>
    private static HttpResponseMessage Download(byte[] contents)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(contents) };
        response.Headers.Add("Dropbox-API-Result",
            """{".tag": "file", "name": "a.jpg", "id": "id:a", "path_display": "/Camera Uploads/a.jpg", "client_modified": "2020-01-01T00:00:00Z", "server_modified": "2020-01-01T00:00:00Z", "rev": "0123456789abcdef", "size": 3}""");

        return response;
    }

    /// <summary>
    /// Answers each download with the response for its attempt, counted from 1.
    /// </summary>
    private sealed class FakeDropboxHandler(Func<int, HttpResponseMessage> respond) : HttpMessageHandler
    {
        private int _downloads;

        public int Downloads => Volatile.Read(ref _downloads);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.EndsWith("/files/download", request.RequestUri!.AbsolutePath);

            return Task.FromResult(respond(Interlocked.Increment(ref _downloads)));
        }
    }
}
