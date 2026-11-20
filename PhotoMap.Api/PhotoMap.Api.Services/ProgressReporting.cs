namespace PhotoMap.Api.Services;

/// <summary>
/// Reports the counters of a run as it goes, a step of <see cref="ProcessingProgress.ReportStep"/> files at a time.
/// </summary>
public static class ProgressReporting
{
    /// <summary>
    /// How often the counters are checked for a step crossed; the files of a step are reported together however fast
    /// they come.
    /// </summary>
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Makes the report given whenever the run has got a step further, see <see cref="ProcessingProgress.GetReportStep"/>,
    /// until the run has finished. The run reports where it starts from, and where it ends at, itself.
    /// </summary>
    /// <param name="getProgress">The counters of the run, which a run may replace once it has listed its files: the
    /// counters replaced are reported too.</param>
    public static async Task ReportOnStepsAsync(Func<ProcessingProgress> getProgress, Func<Task> reportAsync,
        CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(CheckInterval);

        var lastProgress = getProgress();
        var lastStep = lastProgress.GetReportStep();

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                var progress = getProgress();
                var step = progress.GetReportStep();

                if (ReferenceEquals(progress, lastProgress) && step == lastStep)
                {
                    continue;
                }

                lastProgress = progress;
                lastStep = step;

                await reportAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // processing has finished
        }
    }
}
