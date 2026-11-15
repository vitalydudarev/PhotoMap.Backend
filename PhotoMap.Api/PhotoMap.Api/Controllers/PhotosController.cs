using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.DTOs;
using PhotoMap.Api.Services.Interfaces;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Controllers
{
    [ApiController]
    [Route("api/photos")]
    public class PhotosController : ControllerBase
    {
        private readonly IPhotoProvider _photoProvider;
        private readonly IPhotoService _photoService;

        public PhotosController(IPhotoProvider photoProvider, IPhotoService photoService)
        {
            _photoProvider = photoProvider;
            _photoService = photoService;
        }

        [HttpGet("{id:long}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetPhotoAsync(long id, CancellationToken cancellationToken)
        {
            var photoFile = await _photoProvider.GetPhotoAsync(id, cancellationToken);
            if (photoFile != null)
            {
                return new FileContentResult(photoFile.Contents, photoFile.ContentType);
            }

            return NotFound();
        }
        
        /// <summary>
        /// The EXIF the worker read from the photo, as it saved it: an object of the EXIF directories, each an object
        /// of its tags. An empty object when the photo has no EXIF.
        /// </summary>
        /// <param name="id">The ID of the photo.</param>
        [HttpGet("{id:long}/exif")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetExifAsync(long id)
        {
            var photo = await _photoService.GetAsync(id);
            if (photo == null)
            {
                return NotFound();
            }

            // already JSON, so it is sent as it is rather than serialized again into a string
            return Content(string.IsNullOrEmpty(photo.ExifString) ? "{}" : photo.ExifString, "application/json");
        }

        /// <summary>
        /// The address of the GPS location of the photo, by its reverse geocoding: of the feature of the type
        /// "address", or of the first feature when there is none of that type. Null when the photo has not been
        /// geocoded (yet), or nothing was found at its location.
        /// </summary>
        /// <param name="id">The ID of the photo.</param>
        [HttpGet("{id:long}/address")]
        [ProducesResponseType(typeof(PhotoAddressDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetAddressAsync(long id)
        {
            var photo = await _photoService.GetAsync(id);
            if (photo == null)
            {
                return NotFound();
            }

            return Ok(new PhotoAddressDto(GeocodedAddress.Get(photo.Geocoding)));
        }

        [HttpGet("{id:long}/thumb/{size:regex(^(small|large)$)}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetThumbAsync(long id, string size)
        {
            var fileContents = await _photoProvider.GetThumbAsync(id, size);
            if (fileContents != null)
            {
                return new FileContentResult(fileContents, "image/jpeg");
            }

            return NotFound();
        }
    }
}
