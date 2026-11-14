namespace PhotoMap.Api.Database.Entities;

/// <summary>
/// A task that fixes the data once, recorded when it has completed, so that it is not run again.
/// </summary>
public class CompletedMaintenanceTaskEntity
{
    public required string Name { get; set; }
    public DateTimeOffset CompletedOn { get; set; }
}
