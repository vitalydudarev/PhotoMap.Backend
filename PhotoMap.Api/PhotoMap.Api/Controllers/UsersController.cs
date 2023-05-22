using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PhotoMap.Api.Domain.Models;
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
        private readonly IUserPhotoSourceService _userPhotoSourceService;

        public UsersController(IPhotoService photoService, IUserService dbUserService, IUserPhotoSourceService userPhotoSourceService, HostInfo hostInfo)
        {
            _photoService = photoService;
            _dbUserService = dbUserService;
            _userPhotoSourceService = userPhotoSourceService;
            _hostInfo = hostInfo;
        }

        [HttpGet("{id:long}")]
        [ProducesResponseType(typeof(User), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUser(long id)
        {
            var user = await _dbUserService.GetAsync(id);
            if (user != null)
            {
                return Ok(user);
            }

            return NotFound();
        }
        
        [HttpGet("{id:long}/photo-source-settings")]
        [ProducesResponseType(typeof(User), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUserPhotoSourceSettings(long id)
        {
            var userPhotoSourceSettings = await _userPhotoSourceService.GetUserPhotoSourceSettings(id);

            var dtos = userPhotoSourceSettings.Select(a => new UserPhotoSourceSettingsDto
            {
                UserId = a.UserId,
                PhotoSourceId = a.PhotoSourceId,
                PhotoSourceName = a.PhotoSourceName,
                IsUserAuthorized = a.IsUserAuthorized,
                AuthResult = a.AuthSettings != null ? new AuthResultDto
                {
                    Token = a.AuthSettings.Token,
                    TokenExpiresOn = a.AuthSettings.TokenExpiresOn
                } : null
            });

            return Ok(dtos);
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> AddUserAsync([FromBody] AddUserDto addUserDto)
        {
            await _dbUserService.AddAsync(addUserDto.Name);

            return Ok();
        }

        [HttpPatch("{id:long}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateUserAsync(long id, [FromBody] UpdateUserDto updateUserDto)
        {
            await _dbUserService.UpdateAsync(id, updateUserDto.YandexDiskToken, updateUserDto.YandexDiskTokenExpiresIn,
                updateUserDto.YandexDiskStatus, updateUserDto.DropboxToken, updateUserDto.DropboxTokenExpiresIn,
                updateUserDto.DropboxStatus);

            return Ok();
        }

        [HttpGet("{id}/photos")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedResponse<PhotoDto>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetUserPhotos([FromRoute] int id, [FromQuery] int top, [FromQuery] int skip)
        {
            var userPhotos = await _photoService.GetByUserIdAsync(id, top, skip);
            var totalPhotosCount = await _photoService.GetTotalCountByUserIdAsync(id);
            
            static string Source(string s) =>
                s switch
                {
                    "Yandex.Disk" => "yandex-disk",
                    "Dropbox" => "dropbox",
                    _ => s
                };
            
            var url = _hostInfo.GetUrl() + "api";

            var values = userPhotos.Select(a => new PhotoDto
            {
                DateTimeTaken = a.DateTimeTaken.UtcDateTime,
                FileName = a.FileName,
                Id = a.Id,
                Latitude = a.Latitude,
                Longitude = a.Longitude,
                PhotoUrl = $"{url}/{Source("a.Source")}/photos/" + a.Id,
                ThumbnailLargeUrl = $"{url}/photos/{a.Id}/thumb/large",
                ThumbnailSmallUrl = $"{url}/photos/{a.Id}/thumb/small"
            }).ToArray();

            var response = new PagedResponse<PhotoDto> { Values = values, Limit = top, Offset = skip, Total = totalPhotosCount };
            
            return Ok(response);
        }
    }
}
