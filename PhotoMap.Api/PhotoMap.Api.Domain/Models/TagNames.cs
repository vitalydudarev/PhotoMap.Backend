using System.Text.RegularExpressions;

namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// How the names of the tags are written: trimmed, with single spaces between words, and told apart whatever their
/// case.
/// </summary>
public static partial class TagNames
{
    /// <summary>
    /// The longest name of a tag.
    /// </summary>
    public const int MaxLength = 50;

    /// <summary>
    /// The most tags a photo has.
    /// </summary>
    public const int MaxPerPhoto = 30;

    /// <returns>The name trimmed, with single spaces between its words.</returns>
    public static string Clean(string name)
    {
        return Whitespace().Replace(name.Trim(), " ");
    }

    /// <returns>What tells the name apart from the others: Paris and paris are the same tag.</returns>
    public static string Normalize(string name)
    {
        return Clean(name).ToLowerInvariant();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
