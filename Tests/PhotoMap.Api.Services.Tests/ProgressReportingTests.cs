namespace PhotoMap.Api.Services.Tests;

public class ProgressReportingTests
{
    [Fact]
    public async Task ReportOnStepsAsync_ShouldReport_OnlyWhenAStepIsCrossed()
    {
        // Arrange
        var progress = new ProcessingProgress(0, 0) { TotalCount = 500 };
        var reports = 0;
        using var cancellationTokenSource = new CancellationTokenSource();

        var reporting = ProgressReporting.ReportOnStepsAsync(() => progress, () =>
        {
            Interlocked.Increment(ref reports);
            return Task.CompletedTask;
        }, cancellationTokenSource.Token);

        // Act: a few files, short of a step, are not reported
        for (var i = 0; i < 10; i++)
        {
            progress.FileProcessed();
        }

        await Task.Delay(600);
        var reportsWithinAStep = Volatile.Read(ref reports);

        for (var i = 0; i < ProcessingProgress.ReportStep; i++)
        {
            progress.FileProcessed();
        }

        await WaitUntilAsync(() => Volatile.Read(ref reports) > 0);

        await cancellationTokenSource.CancelAsync();
        await reporting;

        // Assert
        Assert.Equal(0, reportsWithinAStep);
        Assert.Equal(1, reports);
    }

    [Fact]
    public async Task ReportOnStepsAsync_ShouldReport_WhenTheProgressIsReplaced()
    {
        // Arrange: a run replaces its counters once it has listed its files
        var progress = new ProcessingProgress(0, 0);
        var reports = 0;
        using var cancellationTokenSource = new CancellationTokenSource();

        var reporting = ProgressReporting.ReportOnStepsAsync(() => progress, () =>
        {
            Interlocked.Increment(ref reports);
            return Task.CompletedTask;
        }, cancellationTokenSource.Token);

        // Act
        progress = new ProcessingProgress(0, 0);

        await WaitUntilAsync(() => Volatile.Read(ref reports) > 0);

        await cancellationTokenSource.CancelAsync();
        await reporting;

        // Assert
        Assert.Equal(1, reports);
    }

    [Fact]
    public async Task ReportOnStepsAsync_ShouldReportTheFilesShortOfAStep_OnceTheFallbackIntervalHasPassed()
    {
        // Arrange: a slow run, short of a step
        var progress = new ProcessingProgress(0, 0) { TotalCount = 500 };
        var reports = 0;
        using var cancellationTokenSource = new CancellationTokenSource();

        var reporting = ProgressReporting.ReportOnStepsAsync(() => progress, () =>
        {
            Interlocked.Increment(ref reports);
            return Task.CompletedTask;
        }, cancellationTokenSource.Token, TimeSpan.FromMilliseconds(500));

        // Act
        progress.FileProcessed();

        await WaitUntilAsync(() => Volatile.Read(ref reports) > 0);

        // nothing has changed since, nothing more is reported
        await Task.Delay(1000);

        await cancellationTokenSource.CancelAsync();
        await reporting;

        // Assert
        Assert.Equal(1, reports);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }
    }
}
