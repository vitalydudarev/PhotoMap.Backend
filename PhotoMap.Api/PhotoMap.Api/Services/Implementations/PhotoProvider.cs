using System;
using System.Threading.Tasks;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Interfaces;

namespace PhotoMap.Api.Services.Implementations;

public class PhotoProvider : IPhotoProvider
{
    private readonly IPhotoService _photoService;
    private readonly IFileStorage _fileStorage;

    public PhotoProvider(IPhotoService photoService, IFileStorage fileStorage)
    {
        _photoService = photoService;
        _fileStorage = fileStorage;
    }
        
    public async Task<byte[]?> GetPhotoAsync(long id)
    {
        var photo = await _photoService.GetAsync(id);
        if (photo == null)
        {
            throw new Exception("Photo not found");
        }

        // if (photo.Source == "Yandex.Disk")
        // {
            // var user = photo.User;
            // if (user == null)
            // {
                // throw new Exception("User not found");
            // }
            
            // TODO: implement
        // }

        throw new NotImplementedException();
    }
        
    public async Task<byte[]?> GetThumbAsync(long id, string size)
    {
        var photo = await _photoService.GetAsync(id);
        if (photo != null)
        {
            var filePath = size == "small" ? photo.ThumbnailSmallFilePath : photo.ThumbnailLargeFilePath;

            return await _fileStorage.GetAsync(filePath);
        }

        return null;
    }
}