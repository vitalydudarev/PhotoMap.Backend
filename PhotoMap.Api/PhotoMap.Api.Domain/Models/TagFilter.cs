namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// The tags the videos are taken by.
/// </summary>
/// <param name="TagIds">The tags they have, any of them. With <paramref name="Untagged"/>, those with no tag are taken
/// too. Those with and without tags when neither is given.</param>
/// <param name="Untagged">Whether those with no tag are taken, along with those of <paramref name="TagIds"/>; only
/// they are when no tag is given.</param>
public record TagFilter(IReadOnlyCollection<long> TagIds, bool Untagged = false)
{
    public static TagFilter All { get; } = new([]);

    /// <summary>
    /// Whether they are narrowed down by their tags.
    /// </summary>
    public bool FiltersByTags => TagIds.Count > 0 || Untagged;
}
