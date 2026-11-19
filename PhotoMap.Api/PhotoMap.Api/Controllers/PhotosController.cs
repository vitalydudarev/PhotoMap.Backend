using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
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
        /// <summary>
        /// How long the browser keeps the preview of a photo.
        /// </summary>
        public static readonly TimeSpan PreviewMaxAge = TimeSpan.FromDays(7);

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
                return ToInlineFile(photoFile);
            }

            return NotFound();
        }

        /// <summary>
        /// The photo to be viewed: the XXXL preview Yandex.Disk makes of it, much smaller than the photo, or the
        /// photo itself when its source makes no such preview or has not made it of the photo. The browser keeps it for
        /// <see cref="PreviewMaxAge"/>: a photo does not change, so it is not downloaded again as the user goes back
        /// and forth between photos.
        /// </summary>
        /// <param name="id">The ID of the photo.</param>
        [HttpGet("{id:long}/preview")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetPreviewAsync(long id, CancellationToken cancellationToken)
        {
            var preview = await _photoProvider.GetPreviewAsync(id, cancellationToken);
            if (preview != null)
            {
                // private: the photos of a user are not to be kept by shared caches
                Response.Headers.CacheControl = $"private, max-age={(int)PreviewMaxAge.TotalSeconds}";
                SetInline(preview.FileName);

                // disposed once it has been sent, which ends the download from the source
                return new FileStreamResult(preview.Contents, preview.ContentType);
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

        /// <summary>
        /// The photo is shown by the browser rather than downloaded, and saved under its file name rather than under
        /// the last segment of the URL, its ID.
        /// </summary>
        private FileContentResult ToInlineFile(PhotoFile photoFile)
        {
            SetInline(photoFile.FileName);

            return new FileContentResult(photoFile.Contents, photoFile.ContentType);
        }

        private void SetInline(string fileName)
        {
            var contentDisposition = new ContentDispositionHeaderValue("inline");
            contentDisposition.SetHttpFileName(fileName);
            Response.Headers.ContentDisposition = contentDisposition.ToString();
        }
    }
}
