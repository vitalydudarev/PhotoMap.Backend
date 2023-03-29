using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Interfaces;

namespace PhotoMap.Api.Controllers
{
    [ApiController]
    [Route("api/photos")]
    public class PhotosController : ControllerBase
    {
        private readonly IFileProvider _fileProvider;
        private readonly IPhotoService _photoService;
        private readonly IFileStorage _fileStorage;

        public PhotosController(IFileProvider fileProvider, IPhotoService photoService, IFileStorage fileStorage)
        {
            _fileProvider = fileProvider;
            _photoService = photoService;
            _fileStorage = fileStorage;
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetPhotoAsync(long id)
        {
            var fileContents = await _fileProvider.GetFileContents(id);

            return new FileContentResult(fileContents, "image/jpg");
        }
        
        [HttpGet("{id:int}/thumb/{size}")]
        public async Task<IActionResult> GetThumbAsync(int id, string size)
        {
            var photo = await _photoService.GetAsync(id);
            
            var filePath = size == "small" ? photo.ThumbnailSmallFilePath : photo.ThumbnailLargeFilePath;

            var bytes = await _fileStorage.GetAsync(filePath);

            return new FileContentResult(bytes, "image/jpg");
        }
    }
}
