using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using PhotoMap.Api.Domain.Services;

namespace PhotoMap.Api.Controllers
{
    [ApiController]
    [Route("api/data")]
    public class DataController : ControllerBase
    {
        private readonly IPhotoService _photoService;

        public DataController(IPhotoService photoService)
        {
            _photoService = photoService;
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteAllDataAsync()
        {
            await _photoService.DeleteAllAsync();

            return NoContent();
        }
    }
}
