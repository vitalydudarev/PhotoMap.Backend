using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PhotoMap.Api.Services;

namespace PhotoMap.Api.Controllers
{
    [ApiController]
    [Route("api/data")]
    public class DataController : ControllerBase
    {
        private readonly IPhotoSourceDataService _photoSourceDataService;

        public DataController(IPhotoSourceDataService photoSourceDataService)
        {
            _photoSourceDataService = photoSourceDataService;
        }

        /// <summary>
        /// Deletes the data of every photo source of every user: the photos, their thumbnails and the processing
        /// status and state of the sources. The users stay authorized in them.
        /// </summary>
        [HttpDelete]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> DeleteAllDataAsync()
        {
            var deleted = await _photoSourceDataService.DeleteAllDataAsync();
            if (!deleted)
            {
                return Conflict("Processing of a photo source is running.");
            }

            return NoContent();
        }
    }
}
