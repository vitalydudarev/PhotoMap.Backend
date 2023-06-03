using System;
using System.Collections.Generic;
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
    [Route("api/users/{userId:long}/photo-sources")]
    public class UsersPhotoSourcesController : ControllerBase
    {
        private readonly IUserPhotoSourceService _userPhotoSourceService;

        public UsersPhotoSourcesController(IUserPhotoSourceService userPhotoSourceService)
        {
            _userPhotoSourceService = userPhotoSourceService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<UserPhotoSourceDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUserPhotoSources(long userId)
        {
            var userPhotoSourceSettings = await _userPhotoSourceService.GetUserPhotoSourcesAsync(userId);

            var dtos = userPhotoSourceSettings.Select(a => new UserPhotoSourceDto
            {
                UserId = a.UserId,
                PhotoSourceId = a.PhotoSourceId,
                PhotoSourceName = a.PhotoSourceName,
                IsUserAuthorized = a.IsUserAuthorized,
                TokenExpiresOn = a.TokenExpiresOn.HasValue ? DateTime.SpecifyKind(a.TokenExpiresOn.Value, DateTimeKind.Utc).ToString("o") : null
            });

            return Ok(dtos);
        }
        
        [HttpPut("{sourceId:long}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateUserPhotoSource(long userId, long sourceId, [FromBody] AuthResultInputDto authResultInputDto)
        {
            var authResult = new UserAuthSettings
            {
                Token = authResultInputDto.Token,
                TokenExpiresOn = DateTimeOffset.UtcNow.AddSeconds(authResultInputDto.TokenExpiresIn)
            };

            await _userPhotoSourceService.UpdateUserPhotoSourceAuthResultAsync(userId, sourceId, authResult);

            return Ok();
        }
        
        [HttpPost("{sourceId:long}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> SourceProcessing(long userId, long sourceId, [FromBody] AuthResultInputDto authResultInputDto)
        {
            // var authResult = new AuthResult
            // {
                // Token = authResultInputDto.Token,
                // TokenExpiresOn = DateTimeOffset.UtcNow.AddSeconds(authResultInputDto.TokenExpiresIn)
            // };

            // await _userPhotoSourceService.UpdateUserPhotoSourceAuthResultAsync(userId, sourceId, authResult);

            return Ok();
        }
    }
}
