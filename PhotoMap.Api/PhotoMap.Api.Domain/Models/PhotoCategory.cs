namespace PhotoMap.Api.Domain.Models;

/// <summary>
/// What kind of photo a photo is, told from its file and its EXIF. A photo can be in several categories, or in none.
/// </summary>
public enum PhotoCategory
{
    /// <summary>
    /// Not a category of its own, and never saved: the photos in none of the categories, to filter the photos by.
    /// </summary>
    Other = 0,

    /// <summary>
    /// A PNG file.
    /// </summary>
    Screenshot = 1,

    /// <summary>
    /// A photo taken by a DJI camera, or 4000 by 3000 pixels.
    /// </summary>
    DroneFootage = 2,

    /// <summary>
    /// Not a category of its own, and never saved with the categories: the photos the user marked as deleted, kept
    /// until they are deleted for good. The other categories leave them out.
    /// </summary>
    Deleted = 3
}
