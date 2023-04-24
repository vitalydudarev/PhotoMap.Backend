using System.Threading.Tasks;
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
        public async Task<IActionResult> GetPhotoAsync(long id)
        {
            var fileContents = await _photoProvider.GetPhotoAsync(id);
            if (fileContents != null)
            {
                return new FileContentResult(fileContents, "image/jpg");
            }

            return BadRequest();
        }
        
        [HttpGet("{id:int}/thumb/{size}")]
        public async Task<IActionResult> GetThumbAsync(int id, string size)
        {
            var fileContents = await _photoProvider.GetThumbAsync(id, size);
            if (fileContents != null)
            {
                return new FileContentResult(fileContents, "image/jpg");
            }

            return BadRequest();
        }
    }

    
}
