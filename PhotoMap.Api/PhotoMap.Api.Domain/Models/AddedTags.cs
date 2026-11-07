namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// What came of adding tags to several photos of a user at once.
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
    /// The names of the tags of each photo once they were added, by name, by the ID of the photo. Empty unless the
    /// tags were added.
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
    /// Every photo has the tags now.
    /// </summary>
    Added,

    /// <summary>
    /// The user has no photo of one of the IDs; no photo was given the tags.
    /// </summary>
    PhotoNotFound,

    /// <summary>
    /// A photo would have more than <see cref="TagNames.MaxPerPhoto"/> tags; no photo was given the tags.
    /// </summary>
    TooManyTags
}
