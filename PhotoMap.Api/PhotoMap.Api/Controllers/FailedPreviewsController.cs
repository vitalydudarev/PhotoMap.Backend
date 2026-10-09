using System;
using System.Collections.Generic;
using System.IO;
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
    /// <summary>
    /// The files of the photo sources that failed, of the processing of the photos and of the downloads alike, with
    /// the previews downloaded of them in the background.
    /// </summary>
    [ApiController]
    public class FailedPreviewsController : ControllerBase
    {
        private const string FailedFilesTable = "files";
        private const string FailedDownloadsTable = "downloads";

        private readonly IFailedPreviewService _failedPreviewService;
        private readonly IFileStorage _fileStorage;
        private readonly FailedPreviewsSignal _failedPreviewsSignal;
        private readonly HostInfo _hostInfo;

        public FailedPreviewsController(IFailedPreviewService failedPreviewService, IFileStorage fileStorage,
            FailedPreviewsSignal failedPreviewsSignal, HostInfo hostInfo)
        {
            _failedPreviewService = failedPreviewService;
            _fileStorage = fileStorage;
            _failedPreviewsSignal = failedPreviewsSignal;
            _hostInfo = hostInfo;
        }

        /// <summary>
        /// The failed files of the photo source with their previews, the latest failures first.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="sourceId">The ID of the photo source.</param>
        [HttpGet("api/users/{userId:long}/photo-sources/{sourceId:long}/failed-previews")]
        [ProducesResponseType(typeof(IEnumerable<FailedPreviewDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetFailedPreviews(long userId, long sourceId)
        {
            var failedPreviews = await _failedPreviewService.GetAsync(userId, sourceId);

            return Ok(failedPreviews.Select(ToDto));
        }

        /// <summary>
        /// Has the previews of the photo source that are not downloaded tried again now, those given up on as well.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="sourceId">The ID of the photo source.</param>
        [HttpPost("api/users/{userId:long}/photo-sources/{sourceId:long}/failed-previews/retry")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> RetryFailedPreviews(long userId, long sourceId)
        {
            await _failedPreviewService.ResetAsync(userId, sourceId);
            _failedPreviewsSignal.Notify();

            return Ok();
        }

        /// <summary>
        /// The preview of a failed file.
        /// </summary>
        /// <param name="table"><c>files</c> or <c>downloads</c>.</param>
        /// <param name="id">The ID of the failed file.</param>
        [HttpGet("api/failed-previews/{table}/{id:long}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetPreviewAsync(string table, long id)
        {
            FailedPreviewTable? previewTable = table switch
            {
                FailedFilesTable => FailedPreviewTable.FailedFiles,
                FailedDownloadsTable => FailedPreviewTable.FailedDownloads,
                _ => null
            };

            var failedPreview = previewTable != null ? await _failedPreviewService.GetAsync(previewTable.Value, id) : null;
            if (failedPreview?.PreviewFilePath == null)
            {
                return NotFound();
            }

            try
            {
                var contents = await _fileStorage.GetAsync(failedPreview.PreviewFilePath);

                return new FileContentResult(contents, failedPreview.PreviewContentType ?? "image/jpeg");
            }
            catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
            {
                return NotFound();
            }
        }

        private FailedPreviewDto ToDto(FailedPreview failedPreview)
        {
            var table = failedPreview.Table == FailedPreviewTable.FailedFiles ? FailedFilesTable : FailedDownloadsTable;

            return new FailedPreviewDto
            {
                Table = table,
                Id = failedPreview.Id,
                Origin = failedPreview.Table == FailedPreviewTable.FailedFiles
                    ? "Processing"
                    : failedPreview.DownloadKind == SourceDownloadKind.ChosenFolder ? "FolderDownload" : "Download",
                FileName = failedPreview.FileName,
                Folder = failedPreview.Folder,
                Error = failedPreview.Error,
                Attempts = failedPreview.Attempts,
                FailedAt = failedPreview.FailedAt.UtcDateTime.ToString("o"),
                PreviewUrl = failedPreview.PreviewFilePath != null
                    ? $"{_hostInfo.GetUrl()}api/failed-previews/{table}/{failedPreview.Id}"
                    : null,
                PreviewStatus = failedPreview.PreviewFilePath != null
                    ? "Ready"
                    : failedPreview.PreviewAttempts >= FailedPreviewsDownloader.MaxPreviewAttempts ? "Unavailable" : "Pending",
                PreviewError = failedPreview.PreviewError
            };
        }
    }
}
