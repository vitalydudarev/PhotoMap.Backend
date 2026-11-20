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
    private int _listedCount;
    private int _isListing;

    /// <summary>
    /// How many files the counters move by before the run reports them, see <see cref="GetReportStep"/>.
    /// </summary>
    public const int ReportStep = 50;

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
    /// Sets the failed count to the files recorded as failed, which a run counting each failure as it comes runs
    /// ahead of: a file that fails again, in a chunk listed again after a stop, would be counted twice.
    /// </summary>
    public void SetFailedCount(int failedCount) => Volatile.Write(ref _failedCount, failedCount);

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

    /// <summary>
    /// Whether the run is loading the list of the files it takes, which it does before processing any of them.
    /// </summary>
    public bool IsListing => Volatile.Read(ref _isListing) == 1;

    /// <summary>
    /// The files the run has found so far while loading their list.
    /// </summary>
    public int ListedCount => Volatile.Read(ref _listedCount);

    /// <summary>
    /// The run starts loading the list of the files it takes, none of them found yet.
    /// </summary>
    public void StartListing()
    {
        Volatile.Write(ref _listedCount, 0);
        Volatile.Write(ref _isListing, 1);
    }

    /// <summary>
    /// The run has found this many files so far while loading their list.
    /// </summary>
    public void SetListedCount(int listedCount) => Volatile.Write(ref _listedCount, listedCount);

    /// <summary>
    /// The run has loaded the list of the files it takes.
    /// </summary>
    public void FinishListing() => Volatile.Write(ref _isListing, 0);

    /// <summary>
    /// Where the run has got to, in steps of <see cref="ReportStep"/> files: it changes when the files listed, or the
    /// files processed, failed or retried, cross a multiple of it, and when the run starts or finishes listing or
    /// learns how many files it takes. A run reports its counters whenever it changes.
    /// </summary>
    public (bool IsListing, int Listed, int Handled, int Total, int RetryTotal) GetReportStep()
    {
        var isListing = IsListing;

        return (isListing,
            isListing ? ListedCount / ReportStep : 0,
            (ProcessedCount + FailedCount + RetriedCount) / ReportStep,
            TotalCount,
            RetryTotalCount);
    }
}
