namespace PhotoMap.Api.Services.Tests;

public class ProcessingProgressTests
{
    [Fact]
    public void GetReportStep_ShouldStayTheSame_WithinAStepOfFiles()
    {
        // Arrange
        var progress = new ProcessingProgress(0, 0) { TotalCount = 500 };
        var step = progress.GetReportStep();

        // Act
        for (var i = 0; i < ProcessingProgress.ReportStep - 2; i++)
        {
            progress.FileProcessed();
        }

        progress.FileFailed();

        // Assert
        Assert.Equal(step, progress.GetReportStep());
    }

    [Fact]
    public void GetReportStep_ShouldChange_WhenTheFilesHandledCrossAStep()
    {
        // Arrange: the counters of earlier runs are carried on, the step is crossed at the next multiple
        var progress = new ProcessingProgress(ProcessingProgress.ReportStep - 2, 1) { TotalCount = 500 };
        var step = progress.GetReportStep();

        // Act
        progress.FileProcessed();

        // Assert
        Assert.NotEqual(step, progress.GetReportStep());
    }

    [Fact]
    public void GetReportStep_ShouldChange_WhenTheFilesListedCrossAStep()
    {
        // Arrange
        var progress = new ProcessingProgress(0, 0);
        progress.StartListing();
        progress.SetListedCount(ProcessingProgress.ReportStep - 1);
        var step = progress.GetReportStep();

        // Act
        progress.SetListedCount(ProcessingProgress.ReportStep);

        // Assert
        Assert.NotEqual(step, progress.GetReportStep());
        Assert.True(progress.IsListing);
        Assert.Equal(ProcessingProgress.ReportStep, progress.ListedCount);
    }

    [Fact]
    public void GetReportStep_ShouldChange_WhenTheListingStartsAndFinishes()
    {
        // Arrange
        var progress = new ProcessingProgress(0, 0);
        var notListing = progress.GetReportStep();

        // Act
        progress.StartListing();
        var listing = progress.GetReportStep();
        progress.SetListedCount(10);
        progress.FinishListing();

        // Assert
        Assert.NotEqual(notListing, listing);
        Assert.NotEqual(listing, progress.GetReportStep());
        Assert.False(progress.IsListing);
    }
}
