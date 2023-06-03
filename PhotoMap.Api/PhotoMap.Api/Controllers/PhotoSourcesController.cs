using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PhotoMap.Api.Domain.Services;
using PhotoMap.Api.DTOs;

namespace PhotoMap.Api.Controllers;

[ApiController]
[Route("api/photo-sources")]
public class PhotoSourcesController : ControllerBase
{
    private readonly IPhotoSourceService _photoSourceService;

    public PhotoSourcesController(IPhotoSourceService photoSourceService)
    {
        _photoSourceService = photoSourceService;
    }

    [HttpGet("{id:long}/auth-settings")]
    [ProducesResponseType(typeof(AuthSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSourceAuthSettings(long id)
    {
        var clientAuthSettings = await _photoSourceService.GetSourceClientAuthSettingsAsync(id);
        if (clientAuthSettings == null)
        {
            return NotFound();
        }

        var dto = new AuthSettingsDto
        {
            OAuthConfiguration = new OAuthConfigurationDto
            {
                ClientId = clientAuthSettings.OAuthConfiguration.ClientId,
                ResponseType = clientAuthSettings.OAuthConfiguration.ResponseType,
                RedirectUri = clientAuthSettings.OAuthConfiguration.RedirectUri,
                AuthorizeUrl = clientAuthSettings.OAuthConfiguration.AuthorizeUrl,
                TokenUrl = clientAuthSettings.OAuthConfiguration.TokenUrl,
                Scope = clientAuthSettings.OAuthConfiguration.Scope
            },
            RelativeAuthUrl = clientAuthSettings.RelativeAuthUrl
        };

        return Ok(dto);
    }
}