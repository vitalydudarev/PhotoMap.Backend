namespace PhotoMap.Api.Services.Services;

public enum PhotoSourceProcessingCommands
{
    Start = 1,
    Stop = 2,
    /// <summary>
    /// Downloads again the files that failed in earlier runs, instead of listing the source.
    /// </summary>
    RetryFailed = 3,
    /// <summary>
    /// Checks the files of the source uploaded before the last run that has listed it, the ones the runs have been
    /// through, and downloads those that are neither saved nor recorded as failed. The files uploaded since are left to
    /// the next run.
    /// </summary>
    Sync = 4
}