using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.DTOs;

namespace PhotoMap.Api.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly IPhotoService _photoService;
        private readonly IUserService _dbUserService;
        private readonly HostInfo _hostInfo;

        public UsersController(IPhotoService photoService, IUserService dbUserService, HostInfo hostInfo)
        {
            _photoService = photoService;
            _dbUserService = dbUserService;
            _hostInfo = hostInfo;
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> AddUserAsync([FromBody] AddUserDto addUserDto)
        {
            await _dbUserService.AddAsync(addUserDto.Name);

            return Ok();
        }

        [HttpPatch("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateUserAsync(int id, [FromBody] UpdateUserDto updateUserDto)
        {
            await _dbUserService.UpdateAsync(id, updateUserDto.YandexDiskToken, updateUserDto.YandexDiskTokenExpiresIn,
                updateUserDto.YandexDiskStatus, updateUserDto.DropboxToken, updateUserDto.DropboxTokenExpiresIn,
                updateUserDto.DropboxStatus);

            return Ok();
        }

        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUserAsync(int id)
        {
            var user = await _dbUserService.GetAsync(id);
            if (user != null)
                return Ok(user);

            return NotFound();
        }

        [HttpGet("{id}/photos")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedResponse<PhotoDto>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetUserPhotos([FromRoute] int id, [FromQuery] int top, [FromQuery] int skip)
        {
            var userPhotos = await _photoService.GetByUserIdAsync(id, top, skip);
            
            static string Source(string s) =>
                s switch
                {
                    "Yandex.Disk" => "yandex-disk",
                    "Dropbox" => "dropbox",
                    _ => s
                };
            
            var url = _hostInfo.GetUrl() + "api";

            var values = userPhotos.Values.Select(a => new PhotoDto
            {
                DateTimeTaken = a.DateTimeTaken.UtcDateTime,
                FileName = a.FileName,
                Id = a.Id,
                Latitude = a.Latitude,
                Longitude = a.Longitude,
                PhotoUrl = $"{url}/{Source(a.Source)}/photos/" + a.Id,
                ThumbnailLargeUrl = $"{url}/photos/" + a.ThumbnailLargeFileId,
                ThumbnailSmallUrl = $"{url}/photos/" + a.ThumbnailSmallFileId
            }).ToArray();

            var response = new PagedResponse<PhotoDto> { Values = values, Limit = top, Offset = skip, Total = userPhotos.TotalCount };
            
            return Ok(response);
        }
    }
}
