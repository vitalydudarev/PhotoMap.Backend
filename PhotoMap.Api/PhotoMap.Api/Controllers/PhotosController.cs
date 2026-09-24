using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PhotoMap.Api.Services.Interfaces;

namespace PhotoMap.Api.Controllers
{
    [ApiController]
    [Route("api/photos")]
    public class PhotosController : ControllerBase
    {
        private readonly IPhotoProvider _photoProvider;

        public PhotosController(IPhotoProvider photoProvider)
        {
            _photoProvider = photoProvider;
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
