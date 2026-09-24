namespace PhotoMap.Api.Services;

/// <summary>
/// File counters of one photo source processing run. Updated concurrently by the download loop and the
/// image processing pipeline.
/// </summary>
public sealed class ProcessingProgress
{
    private int _processedCount;
    private int _failedCount;

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
    /// A file counted as failed by an earlier run has been saved on a retry.
    /// </summary>
    public void FileRecovered()
    {
        Interlocked.Increment(ref _processedCount);
        Interlocked.Decrement(ref _failedCount);
    }
}
