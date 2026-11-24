namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// What came of adding tags to several photos, or several videos, of a user at once.
/// </summary>
public record AddedTags
{
    private AddedTags(AddedTagsOutcome outcome, IReadOnlyDictionary<long, IReadOnlyList<string>> photoTags)
    {
        Outcome = outcome;
        PhotoTags = photoTags;
    }

    public AddedTagsOutcome Outcome { get; }

    /// <summary>
    /// The names of the tags of each photo, or video, once they were added, by name, by its ID. Empty unless the tags
    /// were added.
    /// </summary>
    public IReadOnlyDictionary<long, IReadOnlyList<string>> PhotoTags { get; }

    public static AddedTags Added(IReadOnlyDictionary<long, IReadOnlyList<string>> photoTags) =>
        new(AddedTagsOutcome.Added, photoTags);

    public static AddedTags PhotoNotFound { get; } =
        new(AddedTagsOutcome.PhotoNotFound, new Dictionary<long, IReadOnlyList<string>>());

    public static AddedTags TooManyTags { get; } =
        new(AddedTagsOutcome.TooManyTags, new Dictionary<long, IReadOnlyList<string>>());
}

public enum AddedTagsOutcome
{
    /// <summary>
    /// Every photo, or video, has the tags now.
    /// </summary>
    Added,

    /// <summary>
    /// The user has no photo, or video, of one of the IDs; none was given the tags.
    /// </summary>
    PhotoNotFound,

    /// <summary>
    /// A photo, or video, would have more than <see cref="TagNames.MaxPerPhoto"/> tags; none was given the tags.
    /// </summary>
    TooManyTags
}
