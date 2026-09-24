namespace PhotoMap.Api.Domain.Models;

public enum PhotoSourceStatus
{
    NotStarted = 1,
    InProgress = 2,
    Done = 3,
    Stopped = 4,
    Failed = 5,
    /// <summary>
    /// The run was interrupted by the application stopping, it resumes where it left off when started again.
    /// </summary>
    Paused = 6
}