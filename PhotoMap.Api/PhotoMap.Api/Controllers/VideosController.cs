using System;
using System.Collections.Generic;
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
        private readonly IFailedFileService _failedFileService;
        private readonly HostInfo _hostInfo;

        public VideosController(IVideoService videoService, IVideoProcessingService videoProcessingService,
            IVideoProvider videoProvider, IFileStorage fileStorage, IFailedFileService failedFileService, HostInfo hostInfo)
        {
            _videoService = videoService;
            _videoProcessingService = videoProcessingService;
            _videoProvider = videoProvider;
            _fileStorage = fileStorage;
            _failedFileService = failedFileService;
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
        /// <param name="year">The years, in UTC, the videos were taken in, repeated for each one, such as
        /// <c>year=2016&amp;year=2019</c>. All the years when not given.</param>
        /// <param name="location">The IDs of the locations the videos were taken in, any of them, repeated for each
        /// one, such as <c>location=12&amp;location=40</c>; see <c>videos/locations</c>. The videos not geocoded are in
        /// no location. The videos taken anywhere, and those with no location, when neither this nor
        /// <c>unknownLocation</c> is given.</param>
        /// <param name="unknownLocation">Whether the videos in no location are taken, along with the ones of
        /// <c>location</c>: those with no GPS, not geocoded yet, or where the geocoding found nothing. Only they are
        /// when no location is given.</param>
        /// <param name="tag">The IDs of the tags the videos have, any of them, repeated for each one, such as
        /// <c>tag=4&amp;tag=9</c>; see <c>photos/tags</c>, as the tags are those of the photos too. The videos with and
        /// without tags when neither this nor <c>untagged</c> is given.</param>
        /// <param name="untagged">Whether the videos with no tag are taken, along with the ones of <c>tag</c>; only
        /// they are when no tag is given.</param>
        [HttpGet("api/users/{userId:long}/videos")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedResponse<VideoDto>))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetUserVideos([FromRoute] long userId,
            [FromQuery, BindRequired, Range(1, MaxPageSize)] int top,
            [FromQuery, Range(0, int.MaxValue)] int skip,
            [FromQuery] PhotoSortOrder sort = PhotoSortOrder.Asc,
            [FromQuery] string[]? folder = null,
            [FromQuery] int[]? year = null,
            [FromQuery] long[]? location = null,
            [FromQuery] bool unknownLocation = false,
            [FromQuery] long[]? tag = null,
            [FromQuery] bool untagged = false)
        {
            var folderPaths = folder ?? [];
            var years = year ?? [];
            var locations = new LocationFilter(location ?? [], unknownLocation);
            var tags = new TagFilter(tag ?? [], untagged);

            var videos = await _videoService.GetByUserIdAsync(userId, folderPaths, years, locations, tags, top, skip,
                sort);
            var total = await _videoService.GetTotalCountByUserIdAsync(userId, folderPaths, years, locations, tags);

            var values = videos.Select(ToDto).ToArray();

            return Ok(new PagedResponse<VideoDto> { Values = values, Limit = top, Offset = skip, Total = total });
        }

        /// <summary>
        /// The videos of the user with a GPS location that the filter takes, every one of them, oldest first, for a
        /// map, which clusters them, so it has no pages. The filter is the one of <c>videos</c>, see there.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="folder">The folders the videos are in, without the file name, any of them.</param>
        /// <param name="year">The years, in UTC, the videos were taken in; all of them when not given.</param>
        /// <param name="location">The IDs of the locations the videos were taken in, any of them.</param>
        /// <param name="unknownLocation">Whether the videos in no location are taken, along with the ones of
        /// <c>location</c>.</param>
        /// <param name="tag">The IDs of the tags the videos have, any of them.</param>
        /// <param name="untagged">Whether the videos with no tag are taken, along with the ones of <c>tag</c>.</param>
        [HttpGet("api/users/{userId:long}/videos/geotagged")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(VideoDto[]))]
        public async Task<IActionResult> GetUserGeotaggedVideos([FromRoute] long userId,
            [FromQuery] string[]? folder = null,
            [FromQuery] int[]? year = null,
            [FromQuery] long[]? location = null,
            [FromQuery] bool unknownLocation = false,
            [FromQuery] long[]? tag = null,
            [FromQuery] bool untagged = false)
        {
            var locations = new LocationFilter(location ?? [], unknownLocation);
            var tags = new TagFilter(tag ?? [], untagged);
            var videos = await _videoService.GetGeotaggedAsync(userId, folder ?? [], year ?? [], locations, tags);

            return Ok(videos.Select(ToDto).ToArray());
        }

        /// <summary>
        /// What the videos the filter takes can still be narrowed down to, for each of its filters: the values of the
        /// videos the filter takes once that filter itself is left out, such as the years of the videos of the folders
        /// picked, so that the options of each filter are those that take some video.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="folder">The folders the videos are in, without the file name, any of them; every folder when
        /// not given.</param>
        /// <param name="year">The years, in UTC, the videos were taken in; all of them when not given.</param>
        /// <param name="location">The IDs of the locations the videos were taken in, any of them.</param>
        /// <param name="unknownLocation">Whether the videos in no location are taken, along with the ones of
        /// <c>location</c>.</param>
        /// <param name="gps">Whether only the videos with a GPS location are counted, as a map shows them; all of them
        /// when not given.</param>
        /// <param name="tag">The IDs of the tags the videos have, any of them.</param>
        /// <param name="untagged">Whether the videos with no tag are taken, along with the ones of <c>tag</c>.</param>
        [HttpGet("api/users/{userId:long}/videos/facets")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(VideoFacets))]
        public async Task<IActionResult> GetUserVideoFacets([FromRoute] long userId,
            [FromQuery] string[]? folder = null,
            [FromQuery] int[]? year = null,
            [FromQuery] long[]? location = null,
            [FromQuery] bool unknownLocation = false,
            [FromQuery] bool gps = false,
            [FromQuery] long[]? tag = null,
            [FromQuery] bool untagged = false)
        {
            var locations = new LocationFilter(location ?? [], unknownLocation);
            var tags = new TagFilter(tag ?? [], untagged);

            return Ok(await _videoService.GetFacetsAsync(userId, folder ?? [], year ?? [], locations, tags, gps));
        }

        /// <summary>
        /// Replaces the tags of a video of the user, which are those of the photos too. The names are trimmed, with
        /// single spaces between words, and a name the user has a tag of, whatever its case, takes that tag; the other
        /// names make new tags.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="videoId">The ID of the video.</param>
        /// <param name="names">The names of the tags, none to take them all off.</param>
        /// <returns>The names of the tags of the video, by name.</returns>
        [HttpPut("api/users/{userId:long}/videos/{videoId:long}/tags")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(string[]))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SetVideoTags([FromRoute] long userId, [FromRoute] long videoId,
            [FromBody] string[] names)
        {
            var cleanNames = names.Select(a => TagNames.Clean(a ?? "")).Where(a => a.Length > 0).ToArray();

            if (cleanNames.Any(a => a.Length > TagNames.MaxLength))
            {
                ModelState.AddModelError(nameof(names), $"A tag is {TagNames.MaxLength} characters long at most.");

                return ValidationProblem(ModelState);
            }

            if (cleanNames.DistinctBy(TagNames.Normalize).Count() > TagNames.MaxPerPhoto)
            {
                ModelState.AddModelError(nameof(names), $"A video has {TagNames.MaxPerPhoto} tags at most.");

                return ValidationProblem(ModelState);
            }

            var tags = await _videoService.SetTagsAsync(userId, videoId, cleanNames);

            return tags != null ? Ok(tags) : NotFound();
        }

        /// <summary>
        /// Adds tags to several videos of the user at once, keeping the tags they have; the tags are those of the
        /// photos too. The names are cleaned as by <c>PUT videos/{videoId}/tags</c>, and a name the user has a tag of,
        /// whatever its case, takes that tag; the other names make new tags. The videos are all given the tags, or none
        /// of them is.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="request">The IDs of the videos, 1 to 1000 of them, and the names of the tags, one at least.</param>
        /// <returns>The tags of each video once they were added.</returns>
        [HttpPost("api/users/{userId:long}/videos/tags")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(VideoTagsDto[]))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AddVideoTags([FromRoute] long userId, [FromBody] AddVideoTagsDto request)
        {
            var videoIds = (request.VideoIds ?? []).Distinct().ToArray();
            var cleanNames = (request.Tags ?? []).Select(a => TagNames.Clean(a ?? "")).Where(a => a.Length > 0)
                .ToArray();

            if (videoIds.Length is 0 or > MaxPageSize)
            {
                ModelState.AddModelError(nameof(request.VideoIds), $"Give 1 to {MaxPageSize} videos.");
            }

            if (cleanNames.Length == 0)
            {
                ModelState.AddModelError(nameof(request.Tags), "Give a tag at least.");
            }
            else if (cleanNames.Any(a => a.Length > TagNames.MaxLength))
            {
                ModelState.AddModelError(nameof(request.Tags), $"A tag is {TagNames.MaxLength} characters long at most.");
            }
            else if (cleanNames.DistinctBy(TagNames.Normalize).Count() > TagNames.MaxPerPhoto)
            {
                ModelState.AddModelError(nameof(request.Tags), $"A video has {TagNames.MaxPerPhoto} tags at most.");
            }

            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var added = await _videoService.AddTagsAsync(userId, videoIds, cleanNames);

            switch (added.Outcome)
            {
                case AddedTagsOutcome.PhotoNotFound:
                    return NotFound();
                case AddedTagsOutcome.TooManyTags:
                    ModelState.AddModelError(nameof(request.Tags),
                        $"A video has {TagNames.MaxPerPhoto} tags at most, and some of the videos would have more.");

                    return ValidationProblem(ModelState);
                default:
                    return Ok(added.PhotoTags
                        .Select(a => new VideoTagsDto { VideoId = a.Key, Tags = a.Value.ToArray() })
                        .ToArray());
            }
        }

        /// <summary>
        /// Suggests tags for a video of the user: those of the photos and the videos taken within a week of it or
        /// within 5 km, the nearer the better, as the tags are the same for both, and the year and the season it was
        /// taken in. The tags the video has are left out.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="videoId">The ID of the video.</param>
        [HttpGet("api/users/{userId:long}/videos/{videoId:long}/tags/suggestions")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(TagSuggestionDto[]))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetVideoTagSuggestions([FromRoute] long userId, [FromRoute] long videoId)
        {
            var suggestions = await _videoService.SuggestTagsAsync(userId, videoId);
            if (suggestions == null)
            {
                return NotFound();
            }

            return Ok(suggestions
                .Select(a => new TagSuggestionDto { Name = a.Name, ByDate = a.ByDate, ByLocation = a.ByLocation })
                .ToArray());
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
        /// The years, in UTC, the videos of the user were taken in, oldest first.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        [HttpGet("api/users/{userId:long}/videos/years")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(int[]))]
        public async Task<IActionResult> GetUserVideoYears([FromRoute] long userId)
        {
            return Ok(await _videoService.GetYearsAsync(userId));
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
        /// The locations the videos of the user were taken in, by level, from the countries down to the postcodes,
        /// and by name, with how many videos were taken in each. A video is in a location once it is geocoded.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        [HttpGet("api/users/{userId:long}/videos/locations")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(VideoLocationDto[]))]
        public async Task<IActionResult> GetUserVideoLocations([FromRoute] long userId)
        {
            var locations = await _videoService.GetLocationsAsync(userId);

            return Ok(locations.Select(a => new VideoLocationDto
            {
                Id = a.Id,
                Level = a.Level.ToString(),
                Name = a.Name,
                Description = a.Description,
                VideoCount = a.Count,
                ParentIds = a.ParentIds.ToArray()
            }).ToArray());
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
        /// The metadata read from the video file, as the EXIF of the photos: an object of its directories, each an
        /// object of its tags. An empty object when the file has none, or it has not been read yet.
        /// </summary>
        /// <param name="id">The ID of the video.</param>
        [HttpGet("api/videos/{id:long}/exif")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetExifAsync(long id)
        {
            var video = await _videoService.GetAsync(id);
            if (video == null)
            {
                return NotFound();
            }

            // already JSON, so it is sent as it is rather than serialized again into a string
            return Content(string.IsNullOrEmpty(video.ExifString) ? "{}" : video.ExifString, "application/json");
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
        /// Starts or stops the processing of the videos of the photo source, or retries only the videos that failed.
        /// Only Yandex.Disk sources have videos processed.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="sourceId">The ID of the photo source.</param>
        /// <param name="command">Start, Stop or RetryFailed.</param>
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

            if (command is not (PhotoSourceProcessingCommands.Start or PhotoSourceProcessingCommands.Stop
                or PhotoSourceProcessingCommands.RetryFailed))
            {
                return BadRequest("The videos of a photo source are only started, stopped and retried.");
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
        /// The videos of the photo source whose previews could not be downloaded or stored, in the order they first
        /// failed. The RetryFailed command of the videos tries them again.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="sourceId">The ID of the photo source.</param>
        [HttpGet("api/users/{userId:long}/photo-sources/{sourceId:long}/videos/failed-files")]
        [ProducesResponseType(typeof(IEnumerable<FailedFileDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetVideoFailedFiles(long userId, long sourceId)
        {
            var failedFiles = await _failedFileService.GetVideosAsync(userId, sourceId);

            return Ok(failedFiles.Select(FailedFileDto.From));
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
                Longitude = video.Longitude,
                Tags = video.Tags.ToArray()
            };
        }
    }
}
