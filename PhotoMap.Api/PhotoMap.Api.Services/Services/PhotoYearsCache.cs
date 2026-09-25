namespace PhotoMap.Api.Services.Services;

/// <summary>
/// The years each user has photos from, read from the database once and then kept up to date as photos are
/// added, so listing them does not go through the photos on every request. Deleting photos can take a year away,
/// which only the database can tell, so it drops the years of the user, to be read again on the next request.
/// </summary>
public class PhotoYearsCache
{
    private readonly object _lock = new();
    private readonly Dictionary<long, SortedSet<int>> _years = new();

    /// <summary>
    /// Counts the changes to the photos of each user, so that years read from the database while the photos
    /// changed are not kept: they may already be out of date.
    /// </summary>
    private readonly Dictionary<long, long> _versions = new();

    /// <param name="load">Reads the years from the database, when they are not cached.</param>
    /// <returns>The years, oldest first.</returns>
    public async Task<IReadOnlyList<int>> GetOrLoadAsync(long userId, Func<Task<IEnumerable<int>>> load)
    {
        long version;

        lock (_lock)
        {
            if (_years.TryGetValue(userId, out var cached))
            {
                return cached.ToArray();
            }

            version = _versions.GetValueOrDefault(userId);
        }

        var loaded = new SortedSet<int>(await load());

        lock (_lock)
        {
            if (_versions.GetValueOrDefault(userId) == version)
            {
                _years[userId] = loaded;
            }
        }

        return loaded.ToArray();
    }

    /// <summary>
    /// Adds the years of photos just saved.
    /// </summary>
    public void AddYears(long userId, IEnumerable<int> years)
    {
        lock (_lock)
        {
            _versions[userId] = _versions.GetValueOrDefault(userId) + 1;

            if (_years.TryGetValue(userId, out var cached))
            {
                cached.UnionWith(years);
            }
        }
    }

    /// <summary>
    /// Drops the years of the user, after photos of theirs were deleted.
    /// </summary>
    public void Invalidate(long userId)
    {
        lock (_lock)
        {
            _versions[userId] = _versions.GetValueOrDefault(userId) + 1;
            _years.Remove(userId);
        }
    }
}
