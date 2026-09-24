using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.Services.Interfaces;
using PhotoMap.Shared.Events;
using PhotoMap.Shared.Messaging.MessageSender;
using PhotoMap.Shared.Yandex.Disk;

namespace PhotoMap.Api.Controllers
{
    [ApiController]
    [Route("api/yandex-disk")]
    public class YandexDiskController : ControllerBase
    {
        private static readonly TimeSpan ConversionTimeout = TimeSpan.FromSeconds(30);

        private readonly IPhotoService _photoService;
        private readonly IMessageSender _messageSender;
        private readonly IConvertedImageHolder _convertedImageHolder;
        private readonly IUserPhotoSourceService _userPhotoSourceService;

        public YandexDiskController(
            IPhotoService photoService,
            IMessageSender messageSender,
            IConvertedImageHolder convertedImageHolder,
            IUserPhotoSourceService userPhotoSourceService)
        {
            _photoService = photoService;
            _messageSender = messageSender;
            _convertedImageHolder = convertedImageHolder;
            _userPhotoSourceService = userPhotoSourceService;
        }

        [HttpGet("photos/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPhotoAsync(int id)
        {
            // TODO: do this as en endpoint of worker
            var photo = await _photoService.GetAsync(id);
            if (photo?.Path == null)
            {
                return NotFound();
            }

            var authResult = await _userPhotoSourceService.GetAuthResultAsync(photo.UserId, photo.PhotoSourceId);
            if (authResult == null || !authResult.IsValid)
            {
                return Unauthorized("Yandex.Disk authorization has expired.");
            }

            using var httpClient = new HttpClient();
            var yandexDiskApiClient = new ApiClient(authResult.Token, httpClient);
            var downloadUrl = await yandexDiskApiClient.GetDownloadUrlAsync(photo.Path, new CancellationToken());

            var bytes = await httpClient.GetByteArrayAsync(downloadUrl.Href);

            if (photo.FileName.ToUpper().EndsWith("HEIC"))
            {
                var commandId = Guid.NewGuid();
                var convertImageCommand = new ConvertImageEvent
                {
                    Id = commandId,
                    FileContents = bytes
                };

                _messageSender.Send(convertImageCommand);

                var convertedBytes = await _convertedImageHolder.WaitAsync(commandId, ConversionTimeout, HttpContext.RequestAborted);
                if (convertedBytes == null)
                {
                    return StatusCode(StatusCodes.Status504GatewayTimeout, "Converting the image timed out.");
                }

                return new FileContentResult(convertedBytes, "image/jpg");
            }

            return new FileContentResult(bytes, "image/jpg");
        }
    }
}
