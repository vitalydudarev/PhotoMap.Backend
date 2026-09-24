namespace PhotoMap.Api.Services.Services;

public enum PhotoSourceProcessingCommands
{
    Start = 1,
    Stop = 2,
    /// <summary>
    /// Downloads again the files that failed in earlier runs, instead of listing the source.
    /// </summary>
    RetryFailed = 3
}