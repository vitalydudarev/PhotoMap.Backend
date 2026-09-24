using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.DTOs;

namespace PhotoMap.Api.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        /// <summary>
        /// The most photos a page holds: the largest page the gallery offers, and the page the map loads by.
        /// </summary>
        private const int MaxPageSize = 1000;

        private readonly IPhotoService _photoService;
        private readonly IUserService _dbUserService;
        private readonly HostInfo _hostInfo;

        public UsersController(IPhotoService photoService, IUserService dbUserService, HostInfo hostInfo)
        {
            _photoService = photoService;
            _dbUserService = dbUserService;
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

        /// <summary>
        /// The photos of the user, by the date they were taken, oldest first unless asked for the other way round.
        /// </summary>
        /// <param name="id">The ID of the user.</param>
        /// <param name="top">How many photos to return, 1 to 1000. Required.</param>
        /// <param name="skip">How many photos to skip, 0 or more, 0 when not given.</param>
        /// <param name="sort">The order of the photos by the date they were taken.</param>
        [HttpGet("{id:long}/photos")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedResponse<PhotoDto>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetUserPhotos([FromRoute] long id,
            [FromQuery, BindRequired, Range(1, MaxPageSize)] int top,
            [FromQuery, Range(0, int.MaxValue)] int skip,
            [FromQuery] PhotoSortOrder sort = PhotoSortOrder.Asc)
        {
            var userPhotos = await _photoService.GetByUserIdAsync(id, top, skip, sort);
            var totalPhotosCount = await _photoService.GetTotalCountByUserIdAsync(id);
            
            var url = _hostInfo.GetUrl() + "api";

            var values = userPhotos.Select(a => new PhotoDto
            {
                DateTimeTaken = a.DateTimeTaken.UtcDateTime,
                FileName = a.FileName,
                Id = a.Id,
                Latitude = a.Latitude,
                Longitude = a.Longitude,
                PhotoUrl = $"{url}/photos/{a.Id}",
                ThumbnailLargeUrl = $"{url}/photos/{a.Id}/thumb/large",
                ThumbnailSmallUrl = $"{url}/photos/{a.Id}/thumb/small"
            }).ToArray();

            var response = new PagedResponse<PhotoDto> { Values = values, Limit = top, Offset = skip, Total = totalPhotosCount };
            
            return Ok(response);
        }
    }
}
