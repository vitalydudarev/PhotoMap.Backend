using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.DTOs;
using PhotoMap.Api.Services;
using PhotoMap.Api.Services.Interfaces;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Controllers
{
    /// <summary>
    /// The videos imported from the photo sources, and their processing, which runs apart from the one of the photos.
    /// </summary>
    [ApiController]
    public class VideosController : ControllerBase
    {
        /// <summary>
        /// The most videos a page holds, the largest page the videos page offers.
        /// </summary>
        private const int MaxPageSize = 1000;

        private readonly IVideoService _videoService;
        private readonly IVideoProcessingService _videoProcessingService;
        private readonly IVideoProvider _videoProvider;
        private readonly IFileStorage _fileStorage;
        private readonly HostInfo _hostInfo;

        public VideosController(IVideoService videoService, IVideoProcessingService videoProcessingService,
            IVideoProvider videoProvider, IFileStorage fileStorage, HostInfo hostInfo)
        {
            _videoService = videoService;
            _videoProcessingService = videoProcessingService;
            _videoProvider = videoProvider;
            _fileStorage = fileStorage;
            _hostInfo = hostInfo;
        }

        /// <summary>
        /// The videos of the user, by the date they were taken, oldest first unless asked for the other way round.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="top">How many videos to return, 1 to 1000. Required.</param>
        /// <param name="skip">How many videos to skip, 0 or more, 0 when not given.</param>
        /// <param name="sort">The order of the videos by the date they were taken.</param>
        /// <param name="folder">The folders to take the videos from, without the file name, repeated for each one,
        /// such as <c>folder=disk:/Camera Uploads&amp;folder=disk:/Videos</c>. All the folders when not given.</param>
        [HttpGet("api/users/{userId:long}/videos")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedResponse<VideoDto>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetUserVideos([FromRoute] long userId,
            [FromQuery, BindRequired, Range(1, MaxPageSize)] int top,
            [FromQuery, Range(0, int.MaxValue)] int skip,
            [FromQuery] PhotoSortOrder sort = PhotoSortOrder.Asc,
            [FromQuery] string[]? folder = null)
        {
            var folderPaths = folder ?? [];

            var videos = await _videoService.GetByUserIdAsync(userId, folderPaths, top, skip, sort);
            var total = await _videoService.GetTotalCountByUserIdAsync(userId, folderPaths);

            var values = videos.Select(ToDto).ToArray();

            return Ok(new PagedResponse<VideoDto> { Values = values, Limit = top, Offset = skip, Total = total });
        }

        /// <summary>
        /// The videos of the user that are copies of one another, of the same size and file name, by their groups,
        /// the groups of the largest videos first. The groups are looked for in the background, a video saved
        /// only a moment ago may not be in one yet.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        [HttpGet("api/users/{userId:long}/videos/duplicates")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(VideoDuplicateGroupDto[]))]
        public async Task<IActionResult> GetUserVideoDuplicates([FromRoute] long userId)
        {
            var videos = await _videoService.GetDuplicatesAsync(userId);

            // the videos come by their groups already
            var groups = videos
                .GroupBy(a => a.DuplicateGroupId!.Value)
                .Select(a => new VideoDuplicateGroupDto
                {
                    Id = a.Key,
                    FileName = a.First().FileName,
                    Size = a.First().Size,
                    Videos = a.Select(ToDto).ToArray()
                })
                .ToArray();

            return Ok(groups);
        }

        /// <summary>
        /// The folders the videos of the user are in, without the file names, by name.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        [HttpGet("api/users/{userId:long}/videos/folders")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(string[]))]
        public async Task<IActionResult> GetUserVideoFolders([FromRoute] long userId)
        {
            return Ok(await _videoService.GetFolderPathsAsync(userId));
        }

        /// <summary>
        /// The video, downloaded from its photo source as it is sent, so that it plays while it is downloading. A
        /// Range header asks for a part of it, which the player does to seek, and is answered with 206 and the
        /// part.
        /// </summary>
        /// <param name="id">The ID of the video.</param>
        [HttpGet("api/videos/{id:long}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status206PartialContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetVideoAsync(long id, CancellationToken cancellationToken)
        {
            RangeHeaderValue.TryParse(Request.Headers.Range.ToString(), out var range);

            var videoStream = await _videoProvider.OpenVideoAsync(id, range, cancellationToken);
            if (videoStream == null)
            {
                return NotFound();
            }

            using var response = videoStream.Response;
            var content = response.Content;

            Response.StatusCode = (int)response.StatusCode;
            // the download server may not know what the file is, the source told when the video was listed
            Response.ContentType = content.Headers.ContentType?.MediaType is { } mediaType && mediaType.StartsWith("video/")
                ? mediaType
                : videoStream.MimeType ?? "application/octet-stream";
            Response.ContentLength = content.Headers.ContentLength;
            Response.Headers.AcceptRanges = "bytes";

            if (content.Headers.ContentRange != null)
            {
                Response.Headers.ContentRange = content.Headers.ContentRange.ToString();
            }

            await using var stream = await content.ReadAsStreamAsync(cancellationToken);
            await stream.CopyToAsync(Response.Body, cancellationToken);

            return new EmptyResult();
        }

        /// <summary>
        /// The preview image of the video, as the photo source made it.
        /// </summary>
        /// <param name="id">The ID of the video.</param>
        [HttpGet("api/videos/{id:long}/preview")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetPreviewAsync(long id)
        {
            var video = await _videoService.GetAsync(id);
            if (video?.PreviewFilePath == null)
            {
                return NotFound();
            }

            try
            {
                var contents = await _fileStorage.GetAsync(video.PreviewFilePath);

                return new FileContentResult(contents, video.PreviewContentType ?? "image/jpeg");
            }
            catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
            {
                return NotFound();
            }
        }

        /// <summary>
        /// Starts or stops the processing of the videos of the photo source. Only Yandex.Disk sources have videos
        /// processed.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="sourceId">The ID of the photo source.</param>
        /// <param name="command">Start or Stop.</param>
        [HttpPost("api/users/{userId:long}/photo-sources/{sourceId:long}/videos")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> VideoProcessing(long userId, long sourceId, [FromBody] PhotoSourceProcessingCommands command)
        {
            if (!await _videoProcessingService.SupportsVideosAsync(sourceId))
            {
                return BadRequest("The videos of the photo source cannot be processed.");
            }

            if (command != PhotoSourceProcessingCommands.Start && command != PhotoSourceProcessingCommands.Stop)
            {
                return BadRequest("The videos of a photo source are only started and stopped.");
            }

            var accepted = await _videoProcessingService.RunCommandAsync(userId, sourceId, command);
            if (!accepted)
            {
                return Conflict("Processing of the videos of the photo source is already running.");
            }

            return Ok();
        }

        /// <summary>
        /// The status of the processing of the videos of the photo source with the number of videos processed,
        /// failed and in total. A running source saves them periodically, the notification hub sends them live.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="sourceId">The ID of the photo source.</param>
        [HttpGet("api/users/{userId:long}/photo-sources/{sourceId:long}/videos/status")]
        [ProducesResponseType(typeof(PhotoSourceProgressDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetVideoStatus(long userId, long sourceId)
        {
            var status = await _videoService.GetStatusAsync(userId, sourceId);

            // the videos of a source that have never been processed have no status saved
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
        /// Deletes the videos imported from the photo source, their previews and the status of their processing.
        /// The user stays authorized in the source.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="sourceId">The ID of the photo source.</param>
        [HttpDelete("api/users/{userId:long}/photo-sources/{sourceId:long}/videos")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> DeleteVideoData(long userId, long sourceId)
        {
            var deleted = await _videoProcessingService.DeleteDataAsync(userId, sourceId);
            if (!deleted)
            {
                return Conflict("Processing of the videos of the photo source is running.");
            }

            return NoContent();
        }

        private VideoDto ToDto(Video video)
        {
            var url = _hostInfo.GetUrl() + "api";

            return new VideoDto
            {
                Id = video.Id,
                PhotoSourceId = video.PhotoSourceId,
                PreviewUrl = $"{url}/videos/{video.Id}/preview",
                VideoUrl = $"{url}/videos/{video.Id}",
                FileName = video.FileName,
                FolderPath = video.FolderPath,
                MimeType = video.MimeType,
                Size = video.Size,
                DateTimeTaken = video.DateTimeTaken.UtcDateTime,
                ExifDateTime = video.ExifDateTime?.UtcDateTime,
                Latitude = video.Latitude,
                Longitude = video.Longitude
            };
        }
    }
}
