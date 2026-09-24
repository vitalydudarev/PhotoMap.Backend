namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// The order photos are listed in, by the date they were taken.
/// </summary>
public enum PhotoSortOrder
{
    /// <summary>
    /// Oldest first.
    /// </summary>
    Asc = 1,

    /// <summary>
    /// Newest first.
    /// </summary>
    Desc = 2
}
