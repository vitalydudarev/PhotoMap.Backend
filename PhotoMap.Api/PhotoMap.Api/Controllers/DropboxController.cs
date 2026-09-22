using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.DTOs;
using PhotoMap.Api.Services;
using PhotoMap.Api.Services.Interfaces;
using PhotoMap.Shared.Events;
using PhotoMap.Shared.Messaging.MessageSender;

namespace PhotoMap.Api.Controllers
{
    [ApiController]
    [Route("api/dropbox")]
    public class DropboxController : ControllerBase
    {
        private static readonly TimeSpan ConversionTimeout = TimeSpan.FromSeconds(30);

        private readonly IPhotoService _photoService;
        private readonly IMessageSender _messageSender;
        private readonly IConvertedImageHolder _convertedImageHolder;
        private readonly IUserPhotoSourceService _userPhotoSourceService;
        private readonly IPhotoSourceService _photoSourceService;

        public DropboxController(
            IPhotoService photoService,
            IMessageSender messageSender,
            IConvertedImageHolder convertedImageHolder,
            IUserPhotoSourceService userPhotoSourceService,
            IPhotoSourceService photoSourceService)
        {
            _photoService = photoService;
            _messageSender = messageSender;
            _convertedImageHolder = convertedImageHolder;
            _userPhotoSourceService = userPhotoSourceService;
            _photoSourceService = photoSourceService;
        }

        [HttpGet("photos/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPhotoAsync(int id)
        {
            var photo = await _photoService.GetAsync(id);
            if (photo == null)
            {
                return NotFound();
            }

            var authResult = await _userPhotoSourceService.GetAuthResultAsync(photo.UserId, photo.PhotoSourceId);
            if (authResult == null || !authResult.IsValid)
            {
                return Unauthorized("Dropbox authorization has expired.");
            }

            var photoSource = await _photoSourceService.GetByIdAsync(photo.PhotoSourceId);

            using var httpClient = new HttpClient();
            using var dropboxClient = DropboxClientFactory.Create(authResult, photoSource.ClientAuthSettings.OAuthConfiguration.ClientId, httpClient);

            // the file ID stays the same when the file is moved or renamed, the path doesn't
            using var fileMetadata = await dropboxClient.Files.DownloadAsync(photo.ExternalId ?? photo.Path);
            var fileContents = await fileMetadata.GetContentAsByteArrayAsync();

            if (photo.FileName.ToUpper().EndsWith("HEIC"))
            {
                var commandId = Guid.NewGuid();
                var convertImageCommand = new ConvertImageEvent
                {
                    Id = commandId,
                    FileContents = fileContents
                };

                _messageSender.Send(convertImageCommand);

                var convertedBytes = await _convertedImageHolder.WaitAsync(commandId, ConversionTimeout, HttpContext.RequestAborted);
                if (convertedBytes == null)
                {
                    return StatusCode(StatusCodes.Status504GatewayTimeout, "Converting the image timed out.");
                }

                return new FileContentResult(convertedBytes, "image/jpg");
            }

            return new FileContentResult(fileContents, "image/jpg");
        }
    }
}
