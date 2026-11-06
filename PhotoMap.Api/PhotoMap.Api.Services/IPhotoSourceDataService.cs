namespace PhotoMap.Api.Services;

public interface IPhotoSourceDataService
{
    /// <returns>false if the photo source is being processed, its data is only deleted between runs.</returns>
    Task<bool> DeleteDataAsync(long userId, long sourceId);

    /// <summary>
    /// Deletes only the camera uploads of the photo source, the photos of the folder it is processed from, with the
    /// status, state and failed files of their processing. The other uploads stay.
    /// </summary>
    /// <returns>false if the camera uploads are being processed, nothing is deleted then.</returns>
    Task<bool> DeleteCameraUploadsDataAsync(long userId, long sourceId);

    /// <summary>
    /// Deletes only the other uploads of the photo source, the photos of the other folders, with the status of
    /// their processing. The camera uploads stay.
    /// </summary>
    /// <returns>false if the other uploads are being processed, nothing is deleted then.</returns>
    Task<bool> DeleteOtherUploadsDataAsync(long userId, long sourceId);

    /// <summary>
    /// Deletes the data of every photo source of every user, as <see cref="DeleteDataAsync"/> does for one.
    /// </summary>
    /// <returns>false if any photo source is being processed, nothing is deleted then.</returns>
    Task<bool> DeleteAllDataAsync();
}
