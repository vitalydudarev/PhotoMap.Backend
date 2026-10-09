namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// Which folder of a photo source a download takes its files from. The downloads of each kind run, and keep their
/// failed files, apart from one another.
/// </summary>
public enum SourceDownloadKind
{
    /// <summary>
    /// The folder the photos of the source are processed from.
    /// </summary>
    ProcessingFolder = 1,

    /// <summary>
    /// A folder the user has chosen, of a source whose folders can be chosen.
    /// </summary>
    ChosenFolder = 2
}
