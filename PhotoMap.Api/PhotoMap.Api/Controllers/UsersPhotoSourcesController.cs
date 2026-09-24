using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.DTOs;
using PhotoMap.Api.Services;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Controllers
{
    [ApiController]
    [Route("api/users/{userId:long}/photo-sources")]
    public class UsersPhotoSourcesController : ControllerBase
    {
        private readonly IUserPhotoSourceService _userPhotoSourceService;
        private readonly IPhotoSourceProcessingService _photoSourceProcessingService;
        private readonly IPhotoSourceDataService _photoSourceDataService;
        private readonly IFailedFileService _failedFileService;

        public UsersPhotoSourcesController(
            IUserPhotoSourceService userPhotoSourceService,
            IPhotoSourceProcessingService photoSourceProcessingService,
            IPhotoSourceDataService photoSourceDataService,
            IFailedFileService failedFileService)
        {
            _userPhotoSourceService = userPhotoSourceService;
            _photoSourceProcessingService = photoSourceProcessingService;
            _photoSourceDataService = photoSourceDataService;
            _failedFileService = failedFileService;
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
                TokenExpiresOn = a.TokenExpiresOn.HasValue ? DateTime.SpecifyKind(a.TokenExpiresOn.Value, DateTimeKind.Utc).ToString("o") : null,
                Status = Enum.Parse<UserPhotoSourceStatusDto>(a.Status.ToString())
            });

            return Ok(dtos);
        }
        
        [HttpPut("{sourceId:long}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateUserPhotoSource(long userId, long sourceId, [FromBody] AuthResultInputDto authResultInputDto)
        {
            var authResult = new UserAuthResult
            {
                Token = authResultInputDto.Token,
                TokenExpiresOn = DateTimeOffset.UtcNow.AddSeconds(authResultInputDto.TokenExpiresIn),
                RefreshToken = authResultInputDto.RefreshToken
            };

            await _userPhotoSourceService.UpdateAuthResultAsync(userId, sourceId, authResult);

            return Ok();
        }
        
        [HttpPost("{sourceId:long}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> SourceProcessing(long userId, long sourceId, [FromBody] PhotoSourceProcessingCommands command)
        {
            var accepted = await _photoSourceProcessingService.RunCommandAsync(userId, sourceId, command);
            if (!accepted)
            {
                return Conflict("Processing of the photo source is already running.");
            }

            return Ok();
        }

        /// <summary>
        /// The status of the processing of the photo source with the number of files processed, failed and in total.
        /// A running source saves them periodically, the notification hub sends them live.
        /// </summary>
        [HttpGet("{sourceId:long}/status")]
        [ProducesResponseType(typeof(PhotoSourceProgressDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetStatus(long userId, long sourceId)
        {
            var status = await _userPhotoSourceService.GetUserPhotoStatusAsync(userId, sourceId);

            // a source that has never been processed has no status saved
            var dto = new PhotoSourceProgressDto
            {
                Status = Enum.Parse<UserPhotoSourceStatusDto>((status?.Status ?? PhotoSourceStatus.NotStarted).ToString()),
                TotalCount = status?.TotalCount ?? 0,
                ProcessedCount = status?.ProcessedCount ?? 0,
                FailedCount = status?.FailedCount ?? 0,
                LastUpdatedAt = status?.LastUpdatedAt?.UtcDateTime.ToString("o")
            };

            return Ok(dto);
        }

        /// <summary>
        /// The files of the photo source that could not be downloaded or processed, in the order they first failed.
        /// The RetryFailed command downloads them again.
        /// </summary>
        [HttpGet("{sourceId:long}/failed-files")]
        [ProducesResponseType(typeof(IEnumerable<FailedFileDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetFailedFiles(long userId, long sourceId)
        {
            var failedFiles = await _failedFileService.GetAsync(userId, sourceId);

            var dtos = failedFiles.Select(a => new FailedFileDto
            {
                ExternalId = a.ExternalId,
                Path = a.Path,
                FileName = a.FileName,
                Stage = a.Stage.ToString(),
                Error = a.Error,
                Attempts = a.Attempts,
                FailedAt = a.FailedAt.UtcDateTime.ToString("o")
            });

            return Ok(dtos);
        }

        /// <summary>
        /// Deletes the photos imported from the photo source and the processing status and state of the source.
        /// The user stays authorized in it.
        /// </summary>
        [HttpDelete("{sourceId:long}/data")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> DeleteData(long userId, long sourceId)
        {
            var deleted = await _photoSourceDataService.DeleteDataAsync(userId, sourceId);
            if (!deleted)
            {
                return Conflict("Processing of the photo source is running.");
            }

            return NoContent();
        }
    }
}
