namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// The files a download of a photo source takes from the folder its photos are processed from.
/// </summary>
[Flags]
public enum SourceDownloadContent
{
    Photos = 1,
    Videos = 2,
    All = Photos | Videos
}
