using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using PhotoMap.Api.Middlewares;
using PhotoMap.Api.Services.Exceptions;

namespace PhotoMap.Api.Services.Tests;

public class ExceptionHandlingMiddlewareTests
{
    public static TheoryData<Exception, int> ExceptionStatusCodes => new()
    {
        { new NotFoundException("Photo with ID 1 not found."), StatusCodes.Status404NotFound },
        { new NotAuthorizedException("User is not authorized."), StatusCodes.Status401Unauthorized },
        { new DropboxException("Access token has expired.", isAuthError: true), StatusCodes.Status401Unauthorized },
        { new YandexDiskException("Unauthorized.", isAuthError: true), StatusCodes.Status401Unauthorized },
        { new DropboxException("Dropbox is down."), StatusCodes.Status502BadGateway },
        { new YandexDiskException("Yandex.Disk is down."), StatusCodes.Status502BadGateway }
    };

    [Theory]
    [MemberData(nameof(ExceptionStatusCodes))]
    public async Task InvokeAsync_ShouldAnswerWithStatusCodeOfException(Exception exception, int statusCode)
    {
        // Arrange
        var context = CreateContext();

        // Act
        await CreateMiddleware(exception).InvokeAsync(context);

        // Assert
        Assert.Equal(statusCode, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);

        var problem = await ReadBodyAsync(context);
        Assert.Equal(statusCode, problem.GetProperty("status").GetInt32());
        Assert.Equal(exception.Message, problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task InvokeAsync_ShouldAnswerWith500WithoutMessage_WhenExceptionIsUnexpected()
    {
        // Arrange
        var context = CreateContext();

        // Act
        await CreateMiddleware(new InvalidOperationException("Connection string: secret")).InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);

        var problem = await ReadBodyAsync(context);
        Assert.DoesNotContain("secret", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task InvokeAsync_ShouldWriteNothing_WhenClientHasGone()
    {
        // Arrange
        var context = CreateContext();
        using var requestAborted = new CancellationTokenSource();
        await requestAborted.CancelAsync();
        context.RequestAborted = requestAborted.Token;

        // Act
        await CreateMiddleware(new OperationCanceledException()).InvokeAsync(context);

        // Assert
        Assert.Equal(0, context.Response.Body.Length);
    }

    private static ExceptionHandlingMiddleware CreateMiddleware(Exception exception)
    {
        return new ExceptionHandlingMiddleware(_ => throw exception, NullLogger<ExceptionHandlingMiddleware>.Instance);
    }

    private static DefaultHttpContext CreateContext()
    {
        return new DefaultHttpContext { Response = { Body = new MemoryStream() } };
    }

    private static async Task<JsonElement> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;

        return await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body);
    }
}
