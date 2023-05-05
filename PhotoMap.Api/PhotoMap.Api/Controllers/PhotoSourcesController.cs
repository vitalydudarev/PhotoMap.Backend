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
}