using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using PhotoMap.Api.Services.Exceptions;
using PhotoMap.Api.Services.Services;
using PhotoMap.Shared.Yandex.Disk;
using PhotoMap.Shared.Yandex.Disk.Models;

namespace PhotoMap.Api.Services.Tests;

public class YandexDiskApiCallsTests
{
    private static readonly Func<int, TimeSpan> NoDelay = _ => TimeSpan.Zero;

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task WrapAsync_ShouldRetry_WhateverTheCallFailsWith(Exception failure)
    {
        // Arrange
        var attempts = 0;

        // Act
        var result = await YandexDiskApiCalls.WrapAsync(NullLogger.Instance, () =>
        {
            attempts++;

            return attempts < 3 ? Task.FromException<int>(failure) : Task.FromResult(42);
        }, retryDelay: NoDelay);

        // Assert
        Assert.Equal(42, result);
        Assert.Equal(3, attempts);
    }

    public static TheoryData<Exception> Failures() => new()
    {
        new HttpRequestException("Connection reset"),
        new ApiException(new ApiError { Error = "InternalServerError" }, HttpStatusCode.InternalServerError),
        new ApiException(new ApiError { Error = "TooManyRequests" }, HttpStatusCode.TooManyRequests),
        // an HTTP client timeout, not the run being stopped
        new TaskCanceledException("The request timed out")
    };

    [Fact]
    public async Task WrapAsync_ShouldFail_WhenTheRetriesAreUsedUp()
    {
        // Arrange
        var attempts = 0;

        // Act & Assert
        await Assert.ThrowsAsync<YandexDiskException>(() => YandexDiskApiCalls.WrapAsync<int>(NullLogger.Instance, () =>
        {
            attempts++;

            throw new HttpRequestException("Connection reset");
        }, retryDelay: NoDelay));

        // the first attempt and 3 retries
        Assert.Equal(4, attempts);
    }

    [Fact]
    public async Task WrapAsync_ShouldNotRetry_WhenTheTokenIsNotAccepted()
    {
        // Arrange
        var attempts = 0;

        // Act
        var exception = await Assert.ThrowsAsync<YandexDiskException>(() => YandexDiskApiCalls.WrapAsync<int>(
            NullLogger.Instance, () =>
            {
                attempts++;

                throw new ApiException(new ApiError { Error = "UnauthorizedError" }, HttpStatusCode.Unauthorized);
            }, retryDelay: NoDelay));

        // Assert
        Assert.True(exception.IsAuthError);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task WrapAsync_ShouldNotRetry_WhenTheRunIsStopped()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var attempts = 0;

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => YandexDiskApiCalls.WrapAsync<int>(NullLogger.Instance, () =>
        {
            attempts++;
            cancellationTokenSource.Cancel();

            throw new OperationCanceledException(cancellationTokenSource.Token);
        }, cancellationTokenSource.Token, NoDelay));

        Assert.Equal(1, attempts);
    }
}
