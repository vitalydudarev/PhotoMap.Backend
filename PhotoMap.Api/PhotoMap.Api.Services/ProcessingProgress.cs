namespace PhotoMap.Api.Services;

/// <summary>
/// File counters of one photo source processing run. Updated concurrently by the download loop and the
/// image processing pipeline.
/// </summary>
public sealed class ProcessingProgress
{
    private int _processedCount;
    private int _failedCount;
    private int _retriedCount;
    private int _recoveredCount;
    private int _retryTotalCount;

    public ProcessingProgress(int processedCount, int failedCount)
    {
        _processedCount = processedCount;
        _failedCount = failedCount;
    }

    public int TotalCount { get; set; }
    public int ProcessedCount => Volatile.Read(ref _processedCount);
    public int FailedCount => Volatile.Read(ref _failedCount);

    public void FileProcessed() => Interlocked.Increment(ref _processedCount);
    public void FileFailed() => Interlocked.Increment(ref _failedCount);

    /// <summary>
    /// The failed files a run retrying them takes, 0 for any other run.
    /// </summary>
    public int RetryTotalCount
    {
        get => Volatile.Read(ref _retryTotalCount);
        set => Volatile.Write(ref _retryTotalCount, value);
    }

    /// <summary>
    /// The files retried so far, saved or failed again.
    /// </summary>
    public int RetriedCount => Volatile.Read(ref _retriedCount);

    /// <summary>
    /// The files retried that have been saved this time.
    /// </summary>
    public int RecoveredCount => Volatile.Read(ref _recoveredCount);

    /// <summary>
    /// A file counted as failed by an earlier run has been saved on a retry.
    /// </summary>
    public void FileRecovered()
    {
        Interlocked.Increment(ref _processedCount);
        Interlocked.Decrement(ref _failedCount);
        Interlocked.Increment(ref _retriedCount);
        Interlocked.Increment(ref _recoveredCount);
    }

    /// <summary>
    /// A file counted as failed by an earlier run has failed again on a retry, it stays counted as failed.
    /// </summary>
    public void FileFailedAgain() => Interlocked.Increment(ref _retriedCount);
}
