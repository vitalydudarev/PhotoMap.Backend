using System.Collections.Generic;
using System.Linq;
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

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PhotoSourceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSources()
    {
        var sources = await _photoSourceService.GetAsync();
        var dtos = sources.Select(a => new PhotoSourceDto { Id = a.Id, Name = a.Name });

        return Ok(dtos);
    }
    
    [HttpGet("{id:long}/auth-settings")]
    [ProducesResponseType(typeof(OAuthSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSourceAuthSettings(long id)
    {
        var oAuthSettings = await _photoSourceService.GetSourceAuthSettingsAsync(id);
        if (oAuthSettings == null)
        {
            return NotFound();
        }

        var dto = new OAuthSettingsDto
        {
            ClientId = oAuthSettings.ClientId,
            ResponseType = oAuthSettings.ResponseType,
            RedirectUri = oAuthSettings.RedirectUri,
            AuthorizeUrl = oAuthSettings.AuthorizeUrl,
            TokenUrl = oAuthSettings.TokenUrl
        };

        return Ok(dto);
    }
}