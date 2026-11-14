using System.Text.RegularExpressions;
using PhotoMap.Api.Domain.Models;

namespace PhotoMap.Api.Services.Services;

/// <summary>
/// Tells the photos of a user that are copies of one another, whatever their folders or photo sources:
/// <list type="bullet">
/// <item>of the same contents, to the byte, by the hash taken of them, whatever their names;</item>
/// <item>named after the same time they were taken, as camera uploads are, whatever the separators of the time or
/// the case of the name: "2013-06-09 17.40.57.jpg" and "2013-06-09 17-40-57.JPG".</item>
/// </list>
/// A copy of a copy is a copy too, so the groups take in the photos matched either way. A photo marked as deleted is
/// a copy of none, so deleting a copy leaves the others alone in their group, and out of it once only one is left.
/// </summary>
public static partial class PhotoDuplicateFinder
{
    /// <returns>The group of duplicates of each photo, the ID of the first photo of the group; null for a photo
    /// without copies.</returns>
    public static IReadOnlyDictionary<long, long?> FindDuplicateGroups(IReadOnlyCollection<PhotoDuplicateKey> photos)
    {
        var duplicateGroupIds = photos.ToDictionary(a => a.Id, _ => (long?)null);
        var groups = new DisjointSets();

        var candidates = photos.Where(a => !a.IsDeleted).ToList();

        var sameContents = candidates
            .Where(a => !string.IsNullOrEmpty(a.ContentHash))
            .GroupBy(a => (a.UserId, Key: a.ContentHash!));

        var sameTimeNames = candidates
            .Select(a => (Photo: a, Name: GetTimeName(a.FileName)))
            .Where(a => a.Name != null)
            .GroupBy(a => (a.Photo.UserId, Key: a.Name!), a => a.Photo);

        foreach (var copies in sameContents.Concat(sameTimeNames).Where(a => a.Count() > 1))
        {
            var first = copies.First().Id;

            foreach (var photo in copies)
            {
                groups.Union(first, photo.Id);
            }
        }

        foreach (var group in candidates.Where(a => groups.Contains(a.Id)).GroupBy(a => groups.Find(a.Id)))
        {
            var duplicateGroupId = group.Min(a => a.Id);

            foreach (var photo in group)
            {
                duplicateGroupIds[photo.Id] = duplicateGroupId;
            }
        }

        return duplicateGroupIds;
    }

    /// <returns>The name of a file named after the time it was taken, the separators of the time and the case made
    /// the same; null for a file named otherwise. What follows the time is kept: "-1" tells a second photo taken
    /// within the same second, and the extension another kind of file.</returns>
    public static string? GetTimeName(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return null;
        }

        var match = TimeNameRegex().Match(fileName);
        if (!match.Success)
        {
            return null;
        }

        var rest = match.Groups["rest"].Value.ToLowerInvariant();
        if (rest.EndsWith(".jpeg"))
        {
            rest = rest[..^".jpeg".Length] + ".jpg";
        }

        return $"{match.Groups["date"].Value} {match.Groups["h"].Value}.{match.Groups["m"].Value}.{match.Groups["s"].Value}{rest}";
    }

    [GeneratedRegex(@"^(?<date>\d{4}-\d{2}-\d{2}) (?<h>\d{2})[.\-](?<m>\d{2})[.\-](?<s>\d{2})(?<rest>.*)$")]
    private static partial Regex TimeNameRegex();

    /// <summary>
    /// Merges the photos matched either way into one group.
    /// </summary>
    private sealed class DisjointSets
    {
        private readonly Dictionary<long, long> _parents = new();

        public bool Contains(long id) => _parents.ContainsKey(id);

        public long Find(long id)
        {
            var root = id;
            while (_parents.TryGetValue(root, out var parent) && parent != root)
            {
                root = parent;
            }

            // path compression: the photos on the way point to the root straight away
            while (id != root)
            {
                var parent = _parents[id];
                _parents[id] = root;
                id = parent;
            }

            return root;
        }

        public void Union(long a, long b)
        {
            _parents.TryAdd(a, a);
            _parents.TryAdd(b, b);

            var rootA = Find(a);
            var rootB = Find(b);

            if (rootA != rootB)
            {
                _parents[rootB] = rootA;
            }
        }
    }
}
