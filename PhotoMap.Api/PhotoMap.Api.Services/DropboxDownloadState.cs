namespace PhotoMap.Api.Services;

public class DropboxDownloadState
{
    /// <summary>
    /// Dropbox list_folder cursor to continue listing from. Null means listing from the start of the folder.
    /// </summary>
    public string? Cursor { get; set; }
}
