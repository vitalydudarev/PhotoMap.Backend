using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Services;
using PhotoMap.Shared.Messaging;

namespace PhotoMap.Api.Services.Tests;

public class ProcessingAvailabilityTests
{
    private readonly ProcessingAvailability _availability = new(checkedByMonitor: true);
    private readonly Queue<bool> _checks = new();
    private readonly Mock<IPhotoSourceProcessingService> _photoProcessing = new();
    private readonly Mock<IVideoProcessingService> _videoProcessing = new();
    private readonly List<(bool Available, string? Reason)> _notifications = [];

    public ProcessingAvailabilityTests()
    {
        _photoProcessing.Setup(a => a.StopAllRuns()).Returns(2);
        _videoProcessing.Setup(a => a.StopAllRuns()).Returns(1);
    }

    [Fact]
    public void ProcessingAvailability_ShouldBeUnavailable_UntilTheMonitorHasCheckedKafka()
    {
        Assert.False(_availability.IsAvailable);
        Assert.Equal(ProcessingAvailability.CheckingReason, _availability.Reason);
        Assert.True(new ProcessingAvailability(checkedByMonitor: false).IsAvailable);
    }

    [Fact]
    public async Task CheckAsync_ShouldMakeProcessingAvailable_WhenKafkaIsReachableOnStart()
    {
        // Arrange
        var monitor = CreateMonitor(true);

        // Act
        await monitor.CheckAsync();

        // Assert
        Assert.True(_availability.IsAvailable);
        Assert.Null(_availability.Reason);
        Assert.Equal([(true, null)], _notifications);
    }

    [Fact]
    public async Task CheckAsync_ShouldTellWhy_WhenKafkaIsNotReachableOnStart()
    {
        // Arrange
        var monitor = CreateMonitor(false, false);

        // Act
        await monitor.CheckAsync();
        await monitor.CheckAsync();

        // Assert: said once; there is no run to stop yet
        Assert.False(_availability.IsAvailable);
        Assert.Equal([(false, ProcessingAvailability.KafkaUnreachableReason)], _notifications);
        _photoProcessing.Verify(a => a.StopAllRuns(), Times.Never);
    }

    [Fact]
    public async Task CheckAsync_ShouldKeepProcessingAvailable_ThroughOneFailedCheck()
    {
        // Arrange
        var monitor = CreateMonitor(true, false, true);

        // Act
        await monitor.CheckAsync();
        await monitor.CheckAsync();
        var availableAfterTheBlip = _availability.IsAvailable;
        await monitor.CheckAsync();

        // Assert
        Assert.True(availableAfterTheBlip);
        Assert.Equal([(true, null)], _notifications);
        _photoProcessing.Verify(a => a.StopAllRuns(), Times.Never);
    }

    [Fact]
    public async Task CheckAsync_ShouldStopTheRuns_WhenKafkaIsGone_AndMakeProcessingAvailable_WhenItIsBack()
    {
        // Arrange
        var monitor = CreateMonitor(true, false, false, false, true);

        // Act & Assert
        await monitor.CheckAsync();
        await monitor.CheckAsync();
        await monitor.CheckAsync();

        Assert.False(_availability.IsAvailable);
        Assert.Equal(ProcessingAvailability.KafkaUnreachableReason, _availability.Reason);
        _photoProcessing.Verify(a => a.StopAllRuns(), Times.Once);
        _videoProcessing.Verify(a => a.StopAllRuns(), Times.Once);

        await monitor.CheckAsync();
        _photoProcessing.Verify(a => a.StopAllRuns(), Times.Once);

        await monitor.CheckAsync();
        Assert.True(_availability.IsAvailable);

        Assert.Equal([(true, null), (false, ProcessingAvailability.KafkaUnreachableReason), (true, null)], _notifications);
    }

    [Fact]
    public async Task CancelTasks_ShouldCancelTheTasksOfTheNamesGiven_Only()
    {
        // Arrange
        var manager = new BackgroundTaskManager(NullLogger<BackgroundTaskManager>.Instance);
        var cancelled = new List<string>();

        foreach (var name in new[] { "UserId=1-SourceId=1", "Videos-UserId=1-SourceId=1", "Download-UserId=1-SourceId=1" })
        {
            manager.TryStartTask(name, async token =>
            {
                try
                {
                    await Task.Delay(Timeout.Infinite, token);
                }
                catch (OperationCanceledException)
                {
                    lock (cancelled)
                    {
                        cancelled.Add(name);
                    }
                }
            });
        }

        // Act
        var names = manager.CancelTasks(a => !a.StartsWith("Download-", StringComparison.Ordinal));

        // Assert
        Assert.Equal(["UserId=1-SourceId=1", "Videos-UserId=1-SourceId=1"], names.Order());

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (manager.IsRunning("UserId=1-SourceId=1") || manager.IsRunning("Videos-UserId=1-SourceId=1"))
        {
            await Task.Delay(10, timeout.Token);
        }

        Assert.True(manager.IsRunning("Download-UserId=1-SourceId=1"));
        manager.CancelTask("Download-UserId=1-SourceId=1");
    }

    private ProcessingAvailabilityMonitor CreateMonitor(params bool[] checks)
    {
        foreach (var check in checks)
        {
            _checks.Enqueue(check);
        }

        var healthCheck = new Mock<IBrokerHealthCheck>();
        healthCheck.Setup(a => a.IsReachable(It.IsAny<TimeSpan>())).Returns(() => _checks.Dequeue());

        var notifications = new Mock<IFrontendNotificationService>();
        notifications
            .Setup(a => a.SendProcessingAvailabilityAsync(It.IsAny<bool>(), It.IsAny<string?>()))
            .Callback<bool, string?>((available, reason) => _notifications.Add((available, reason)))
            .Returns(Task.CompletedTask);

        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(a => a.GetService(typeof(IPhotoSourceProcessingService))).Returns(_photoProcessing.Object);
        serviceProvider.Setup(a => a.GetService(typeof(IVideoProcessingService))).Returns(_videoProcessing.Object);

        var serviceScope = new Mock<IServiceScope>();
        serviceScope.Setup(a => a.ServiceProvider).Returns(serviceProvider.Object);

        var serviceScopeFactory = new Mock<IServiceScopeFactory>();
        serviceScopeFactory.Setup(a => a.CreateScope()).Returns(serviceScope.Object);

        return new ProcessingAvailabilityMonitor(healthCheck.Object, _availability, serviceScopeFactory.Object,
            notifications.Object, NullLogger<ProcessingAvailabilityMonitor>.Instance);
    }
}
