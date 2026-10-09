namespace PhotoMap.Api.Services.Services;

/// <summary>
/// Paths of Yandex.Disk, such as <c>disk:/Photos/2020/IMG_0001.jpg</c>, and where their files are kept in the file
/// storage. A path a user has typed in ends up as a path on the disk of the backend, so its folders are checked to
/// stay within the folder they are downloaded to.
/// </summary>
public static class YandexDiskPaths
{
    private const string DiskPrefix = "disk:";

    /// <summary>
    /// The path of a folder as Yandex.Disk takes it, from what a user has typed in: <c>disk:/Photos/2020</c> for
    /// <c>Photos/2020</c>, <c>/Photos/2020/</c> or <c>disk:/Photos/2020</c>, and <c>disk:/</c> for the root of the
    /// disk.
    /// </summary>
    /// <returns>null when the path is empty, or has a folder that would leave the folder it is downloaded to, such
    /// as <c>..</c>.</returns>
    public static string? NormalizeFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return null;
        }

        var path = folder.Trim();
        if (path.StartsWith(DiskPrefix, StringComparison.OrdinalIgnoreCase))
        {
            path = path[DiskPrefix.Length..];
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return segments.All(IsSafeSegment) ? DiskPrefix + "/" + string.Join('/', segments) : null;
    }

    /// <summary>
    /// The folder of a file, from its path: <c>disk:/Photos/2020</c> for <c>disk:/Photos/2020/IMG_0001.jpg</c>, and
    /// <c>disk:/</c> for a file at the root of the disk.
    /// </summary>
    public static string GetFolder(string path)
    {
        var lastSlash = path.LastIndexOf('/');
        var folder = lastSlash < 0 ? DiskPrefix : path[..lastSlash];

        return NormalizeFolder(folder) ?? DiskPrefix + "/";
    }

    /// <summary>
    /// The file of the path in the folder given of the file storage, in the folders of the path:
    /// <c>{folder}/Photos/2020/IMG_0001.jpg</c> for <c>disk:/Photos/2020/IMG_0001.jpg</c>.
    /// </summary>
    /// <exception cref="ArgumentException">The path has a folder that would leave the folder given.</exception>
    public static string ToStoragePath(string storageFolder, string path)
    {
        var relativePath = path.StartsWith(DiskPrefix, StringComparison.OrdinalIgnoreCase) ? path[DiskPrefix.Length..] : path;
        var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0 || !segments.All(IsSafeSegment))
        {
            throw new ArgumentException($"{path} is not a path of a file that can be downloaded.", nameof(path));
        }

        return Path.Combine([storageFolder, ..segments]);
    }

    /// <summary>
    /// A folder or file name that stays where it is put: not <c>.</c> or <c>..</c>, and without a backslash, which
    /// Windows takes for a separator.
    /// </summary>
    private static bool IsSafeSegment(string segment)
    {
        return segment is not ("." or "..") && !segment.Contains('\\') && !segment.Contains('\0');
    }
}
