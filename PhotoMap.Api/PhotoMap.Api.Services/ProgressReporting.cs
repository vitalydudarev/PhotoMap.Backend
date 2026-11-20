using System.Diagnostics;

namespace PhotoMap.Api.Services;

/// <summary>
/// Reports the counters of a run as it goes, a step of <see cref="ProcessingProgress.ReportStep"/> files at a time,
/// and the files short of a step once <see cref="FallbackInterval"/> has passed without a report.
/// </summary>
public static class ProgressReporting
{
    /// <summary>
    /// How often the counters are checked for a step crossed; the files of a step are reported together however fast
    /// they come.
    /// </summary>
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// How long the counters of a slow run go unreported at most when they have changed short of a step.
    /// </summary>
    public static readonly TimeSpan FallbackInterval = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Makes the report given whenever the run has got a step further, see <see cref="ProcessingProgress.GetReportStep"/>,
    /// or its counters have changed at all and <paramref name="fallbackInterval"/> has passed since the last report,
    /// until the run has finished. The run reports where it starts from, and where it ends at, itself.
    /// </summary>
    /// <param name="getProgress">The counters of the run, which a run may replace once it has listed its files: the
    /// counters replaced are reported too.</param>
    /// <param name="fallbackInterval"><see cref="FallbackInterval"/> unless given.</param>
    public static async Task ReportOnStepsAsync(Func<ProcessingProgress> getProgress, Func<Task> reportAsync,
        CancellationToken cancellationToken, TimeSpan? fallbackInterval = null)
    {
        using var timer = new PeriodicTimer(CheckInterval);
        var sinceLastReport = Stopwatch.StartNew();
        var interval = fallbackInterval ?? FallbackInterval;

        var lastProgress = getProgress();
        var lastStep = lastProgress.GetReportStep();
        var lastCounters = GetCounters(lastProgress);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var progress = getProgress();
                var step = progress.GetReportStep();
                var counters = GetCounters(progress);

                var stepCrossed = !ReferenceEquals(progress, lastProgress) || step != lastStep;
                var fallbackDue = counters != lastCounters && sinceLastReport.Elapsed >= interval;

                if (!stepCrossed && !fallbackDue)
                {
                    continue;
                }

                lastProgress = progress;
                lastStep = step;
                lastCounters = counters;
                sinceLastReport.Restart();

                await reportAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // processing has finished
        }
    }

    /// <summary>
    /// Every counter of the run as it is, which a report sends.
    /// </summary>
    private static (bool IsListing, int Listed, int Processed, int Failed, int Retried, int Total, int RetryTotal) GetCounters(
        ProcessingProgress progress)
    {
        return (progress.IsListing, progress.ListedCount, progress.ProcessedCount, progress.FailedCount,
            progress.RetriedCount, progress.TotalCount, progress.RetryTotalCount);
    }
}
