namespace PhotoMap.Api.DTOs;

/// <summary>
/// What to download from a folder of a photo source the user has chosen.
/// </summary>
public class FolderDownloadRequestDto
{
    /// <summary>
    /// The path of the folder, such as <c>disk:/Photos/2020</c> or <c>/Photos/2020</c>.
    /// </summary>
    public string Folder { get; set; } = null!;

    public SourceDownloadContentDto Content { get; set; }
}
