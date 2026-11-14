namespace PhotoMap.Api.Domain.Repositories;

/// <summary>
/// Records the tasks that fix the data once, so that each runs until it completes, and never after.
/// </summary>
public interface IMaintenanceTaskRepository
{
    Task<bool> IsCompletedAsync(string name);

    /// <summary>
    /// Records the task as completed; recording it twice does no harm.
    /// </summary>
    Task MarkCompletedAsync(string name);
}
