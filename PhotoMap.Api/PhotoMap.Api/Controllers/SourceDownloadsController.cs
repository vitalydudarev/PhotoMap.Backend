using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PhotoMap.Api.Domain.Models;
using PhotoMap.Api.DTOs;
using PhotoMap.Api.Services;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Controllers
{
    /// <summary>
    /// Downloading the original photos and videos of a photo source into the file storage of the backend: from the
    /// folder its photos are processed from, under <c>download</c>, or from a folder the user chooses, under
    /// <c>download/folder</c>. The downloads of each kind run, and keep their failed files, apart from one another
    /// and from the processing of the source.
    /// </summary>
    [ApiController]
    [Route("api/users/{userId:long}/photo-sources/{sourceId:long}/download")]
    public class SourceDownloadsController : ControllerBase
    {
        private readonly ISourceDownloadService _sourceDownloadService;

        public SourceDownloadsController(ISourceDownloadService sourceDownloadService)
        {
            _sourceDownloadService = sourceDownloadService;
        }

        /// <summary>
        /// Starts downloading the files of the folder the photos of the source are processed from. The files
        /// downloaded whole already are skipped and the missing ones downloaded, those that failed before among them.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="sourceId">The ID of the photo source.</param>
        /// <param name="content">Photos, Videos or All.</param>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Start(long userId, long sourceId, [FromBody] SourceDownloadContentDto content)
        {
            if (!Enum.IsDefined(content))
            {
                return BadRequest("Photos, videos or both are downloaded.");
            }

            if (!await _sourceDownloadService.SupportsDownloadsAsync(sourceId, SourceDownloadKind.ProcessingFolder))
            {
                return BadRequest("The files of the photo source cannot be downloaded.");
            }

            var accepted = await _sourceDownloadService.StartAsync(userId, sourceId, ToContent(content));

            return accepted ? Ok() : Conflict("The download of the photo source is already running.");
        }

        /// <summary>
        /// Starts downloading the files of a folder of the photo source, as the folder its photos are processed from
        /// is downloaded. Only the folders of Yandex.Disk can be chosen. The files are saved in the folders of the
        /// path of the folder.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="sourceId">The ID of the photo source.</param>
        /// <param name="request">The folder and Photos, Videos or All.</param>
        [HttpPost("folder")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> StartFolder(long userId, long sourceId, [FromBody] FolderDownloadRequestDto request)
        {
            if (!Enum.IsDefined(request.Content))
            {
                return BadRequest("Photos, videos or both are downloaded.");
            }

            if (YandexDiskPaths.NormalizeFolder(request.Folder) == null)
            {
                return BadRequest("The folder is empty, or has a folder named \".\" or \"..\" or with a backslash.");
            }

            if (!await _sourceDownloadService.SupportsDownloadsAsync(sourceId, SourceDownloadKind.ChosenFolder))
            {
                return BadRequest("The folders of the photo source cannot be chosen to download from.");
            }

            var accepted = await _sourceDownloadService.StartFolderAsync(userId, sourceId, request.Folder, ToContent(request.Content));

            return accepted ? Ok() : Conflict("The folder download of the photo source is already running.");
        }

        /// <summary>
        /// Starts downloading again only the files of the folder the photos are processed from that failed.
        /// </summary>
        [HttpPost("retry-failed")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public Task<IActionResult> RetryFailed(long userId, long sourceId)
        {
            return RetryFailedAsync(userId, sourceId, SourceDownloadKind.ProcessingFolder);
        }

        /// <summary>
        /// Starts downloading again only the files of chosen folders that failed, whichever folder they are in.
        /// </summary>
        [HttpPost("folder/retry-failed")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public Task<IActionResult> RetryFailedFolder(long userId, long sourceId)
        {
            return RetryFailedAsync(userId, sourceId, SourceDownloadKind.ChosenFolder);
        }

        /// <summary>
        /// The files of the folder the photos are processed from that failed to download and are not downloaded
        /// yet, in the order they first failed.
        /// </summary>
        [HttpGet("failed-files")]
        [ProducesResponseType(typeof(IEnumerable<FailedDownloadDto>), StatusCodes.Status200OK)]
        public Task<IActionResult> GetFailedFiles(long userId, long sourceId)
        {
            return GetFailedFilesAsync(userId, sourceId, SourceDownloadKind.ProcessingFolder);
        }

        /// <summary>
        /// The files of chosen folders that failed to download and are not downloaded yet, in the order they first
        /// failed.
        /// </summary>
        [HttpGet("folder/failed-files")]
        [ProducesResponseType(typeof(IEnumerable<FailedDownloadDto>), StatusCodes.Status200OK)]
        public Task<IActionResult> GetFolderFailedFiles(long userId, long sourceId)
        {
            return GetFailedFilesAsync(userId, sourceId, SourceDownloadKind.ChosenFolder);
        }

        /// <summary>
        /// Stops the download of the folder the photos are processed from. The files downloaded so far are kept.
        /// </summary>
        [HttpPost("stop")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult Stop(long userId, long sourceId)
        {
            _sourceDownloadService.Stop(userId, sourceId, SourceDownloadKind.ProcessingFolder);

            return Ok();
        }

        /// <summary>
        /// Stops the download of a chosen folder. The files downloaded so far are kept.
        /// </summary>
        [HttpPost("folder/stop")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult StopFolder(long userId, long sourceId)
        {
            _sourceDownloadService.Stop(userId, sourceId, SourceDownloadKind.ChosenFolder);

            return Ok();
        }

        /// <summary>
        /// The status of the last download of the folder the photos are processed from with the number of files
        /// downloaded, failed and in total, and the folder they are saved to. The notification hub sends them live.
        /// </summary>
        [HttpGet("status")]
        [ProducesResponseType(typeof(SourceDownloadStatusDto), StatusCodes.Status200OK)]
        public Task<IActionResult> GetStatus(long userId, long sourceId)
        {
            return GetStatusAsync(userId, sourceId, SourceDownloadKind.ProcessingFolder);
        }

        /// <summary>
        /// The status of the last download of a chosen folder, as for the folder the photos are processed from, with
        /// the folder it has downloaded.
        /// </summary>
        [HttpGet("folder/status")]
        [ProducesResponseType(typeof(SourceDownloadStatusDto), StatusCodes.Status200OK)]
        public Task<IActionResult> GetFolderStatus(long userId, long sourceId)
        {
            return GetStatusAsync(userId, sourceId, SourceDownloadKind.ChosenFolder);
        }

        private async Task<IActionResult> RetryFailedAsync(long userId, long sourceId, SourceDownloadKind kind)
        {
            if (!await _sourceDownloadService.SupportsDownloadsAsync(sourceId, kind))
            {
                return BadRequest("The files of the photo source cannot be downloaded.");
            }

            var accepted = await _sourceDownloadService.RetryFailedAsync(userId, sourceId, kind);

            return accepted ? Ok() : Conflict("The download of the photo source is already running.");
        }

        private async Task<IActionResult> GetFailedFilesAsync(long userId, long sourceId, SourceDownloadKind kind)
        {
            var failedDownloads = await _sourceDownloadService.GetFailedDownloadsAsync(userId, sourceId, kind);

            var dtos = failedDownloads.Select(a => new FailedDownloadDto
            {
                Folder = a.Folder,
                FileName = a.FileName,
                Size = a.Size,
                Content = Enum.Parse<SourceDownloadContentDto>(a.Content.ToString()),
                Error = a.Error,
                Attempts = a.Attempts,
                FailedAt = a.FailedAt.UtcDateTime.ToString("o")
            });

            return Ok(dtos);
        }

        private async Task<IActionResult> GetStatusAsync(long userId, long sourceId, SourceDownloadKind kind)
        {
            var status = _sourceDownloadService.GetStatus(userId, sourceId, kind);
            var failedDownloads = await _sourceDownloadService.GetFailedDownloadsAsync(userId, sourceId, kind);

            // a source that has not been downloaded since the backend started has no status
            var dto = new SourceDownloadStatusDto
            {
                Status = Enum.Parse<UserPhotoSourceStatusDto>((status?.Status ?? PhotoSourceStatus.NotStarted).ToString()),
                Content = status != null ? Enum.Parse<SourceDownloadContentDto>(status.Content.ToString()) : null,
                RetryingFailed = status?.RetryingFailed ?? false,
                Folder = status?.Folder,
                AlreadyDownloadedCount = status?.AlreadyDownloadedCount ?? 0,
                FailedFileCount = failedDownloads.Count,
                TotalCount = status?.TotalCount ?? 0,
                ProcessedCount = status?.DownloadedCount ?? 0,
                FailedCount = status?.FailedCount ?? 0,
                LastUpdatedAt = status?.LastUpdatedAt.UtcDateTime.ToString("o"),
                TargetFolder = await _sourceDownloadService.GetTargetFolderAsync(userId, sourceId, kind)
            };

            return Ok(dto);
        }

        private static SourceDownloadContent ToContent(SourceDownloadContentDto content)
        {
            return Enum.Parse<SourceDownloadContent>(content.ToString());
        }
    }
}
