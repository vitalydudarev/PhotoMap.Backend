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
        
        /// <summary>
        /// Starts, stops or retries the failed files of the processing of the camera uploads of the photo source:
        /// the photos of the folder it is processed from, which the source uploads the photos of the camera to.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="sourceId">The ID of the photo source.</param>
        /// <param name="command">Start, Stop or RetryFailed.</param>
        [HttpPost("{sourceId:long}/camera-uploads")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CameraUploadsProcessing(long userId, long sourceId, [FromBody] PhotoSourceProcessingCommands command)
        {
            var accepted = await _photoSourceProcessingService.RunCommandAsync(userId, sourceId, command);
            if (!accepted)
            {
                return Conflict("Processing of the camera uploads of the photo source is already running.");
            }

            return Ok();
        }

        /// <summary>
        /// The status of the processing of the camera uploads of the photo source with the number of files processed,
        /// failed and in total. A running source saves them periodically, the notification hub sends them live.
        /// </summary>
        [HttpGet("{sourceId:long}/camera-uploads/status")]
        [ProducesResponseType(typeof(PhotoSourceProgressDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCameraUploadsStatus(long userId, long sourceId)
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
        /// Starts, stops or retries the failed photos of the processing of the other uploads of the photo source: the
        /// photos outside the folder it is processed from, which runs apart from the processing of the camera uploads.
        /// Starting again skips the photos saved already, so it carries on where it left off. Only Yandex.Disk sources
        /// have their other uploads processed.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="sourceId">The ID of the photo source.</param>
        /// <param name="command">Start, Stop or RetryFailed.</param>
        [HttpPost("{sourceId:long}/other-uploads")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> OtherUploadsProcessing(long userId, long sourceId,
            [FromBody] PhotoSourceProcessingCommands command)
        {
            if (!await _photoSourceProcessingService.SupportsOtherUploadsAsync(sourceId))
            {
                return BadRequest("The photo source has no other uploads.");
            }

            var accepted = await _photoSourceProcessingService.RunOtherUploadsCommandAsync(userId, sourceId, command);
            if (!accepted)
            {
                return Conflict("Processing of the other uploads of the photo source is already running.");
            }

            return Ok();
        }

        /// <summary>
        /// The status of the processing of the other uploads of the photo source, the photos outside the folder it is
        /// processed from, with the number of photos processed, failed and in total. A running source saves them periodically, the
        /// notification hub sends them live.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="sourceId">The ID of the photo source.</param>
        [HttpGet("{sourceId:long}/other-uploads/status")]
        [ProducesResponseType(typeof(PhotoSourceProgressDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetOtherUploadsStatus(long userId, long sourceId)
        {
            var status = await _userPhotoSourceService.GetOtherUploadsStatusAsync(userId, sourceId);

            // the other uploads of a source that have never been processed have no status saved
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
        /// The camera uploads of the photo source that could not be downloaded or processed, in the order they first
        /// failed. The RetryFailed command downloads them again.
        /// </summary>
        [HttpGet("{sourceId:long}/camera-uploads/failed-files")]
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
        /// Deletes the camera uploads imported from the photo source, with the status, state and failed files of
        /// their processing. The other uploads stay, and the user stays authorized in the source.
        /// </summary>
        [HttpDelete("{sourceId:long}/camera-uploads/data")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> DeleteCameraUploadsData(long userId, long sourceId)
        {
            var deleted = await _photoSourceDataService.DeleteCameraUploadsDataAsync(userId, sourceId);
            if (!deleted)
            {
                return Conflict("Processing of the camera uploads of the photo source is running.");
            }

            return NoContent();
        }

        /// <summary>
        /// Deletes the other uploads imported from the photo source, the photos of its other uploads, with the
        /// status of their processing. The camera uploads stay, and the user stays authorized in the source.
        /// </summary>
        [HttpDelete("{sourceId:long}/other-uploads/data")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> DeleteOtherUploadsData(long userId, long sourceId)
        {
            if (!await _photoSourceProcessingService.SupportsOtherUploadsAsync(sourceId))
            {
                return BadRequest("The photo source has no other uploads.");
            }

            var deleted = await _photoSourceDataService.DeleteOtherUploadsDataAsync(userId, sourceId);
            if (!deleted)
            {
                return Conflict("Processing of the other uploads of the photo source is running.");
            }

            return NoContent();
        }

        /// <summary>
        /// Deletes all the photos imported from the photo source, its camera uploads and its other uploads, and the
        /// processing statuses and state of the source. The user stays authorized in it.
        /// </summary>
        [HttpDelete("{sourceId:long}/data")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> DeleteData(long userId, long sourceId)
        {
            var deleted = await _photoSourceDataService.DeleteDataAsync(userId, sourceId);
            if (!deleted)
            {
                return Conflict("Processing of the camera uploads or of the other uploads of the photo source is running.");
            }

            return NoContent();
        }
    }
}
