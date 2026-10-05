namespace PhotoMap.Api.Services.Services;

/// <summary>
/// Puts files in their groups of duplicates.
/// </summary>
public interface IDuplicateGroupsService
{
    /// <summary>
    /// Looks for the duplicates among every file and saves the groups of the ones whose group has changed.
    /// </summary>
    /// <returns>How many files have changed their group.</returns>
    Task<int> UpdateDuplicateGroupsAsync();
}
