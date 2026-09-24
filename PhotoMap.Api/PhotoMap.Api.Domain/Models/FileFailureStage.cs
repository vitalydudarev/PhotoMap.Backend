namespace PhotoMap.Api.Domain.Models;

public enum FileFailureStage
{
    /// <summary>
    /// Downloading the file from the photo source failed.
    /// </summary>
    Download = 1,
    /// <summary>
    /// The file was downloaded, storing it, making its thumbnails or saving the photo failed.
    /// </summary>
    Processing = 2
}
