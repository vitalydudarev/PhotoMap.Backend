using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PhotoMap.Api.DTOs;
using PhotoMap.Api.Services.Services;

namespace PhotoMap.Api.Controllers
{
    [ApiController]
    [Route("api/processing")]
    public class ProcessingController : ControllerBase
    {
        private readonly ProcessingAvailability _processingAvailability;

        public ProcessingController(ProcessingAvailability processingAvailability)
        {
            _processingAvailability = processingAvailability;
        }

        /// <summary>
        /// Whether photos and videos can be processed. With Kafka enabled, only while it can be reached; the
        /// notification hub sends the changes live, as ProcessingAvailability. Starting processing while it is
        /// unavailable is answered with 503 Service Unavailable; stopping it always works.
        /// </summary>
        [HttpGet("availability")]
        [ProducesResponseType(typeof(ProcessingAvailabilityDto), StatusCodes.Status200OK)]
        public IActionResult GetAvailability()
        {
            return Ok(new ProcessingAvailabilityDto
            {
                Available = _processingAvailability.IsAvailable,
                Reason = _processingAvailability.Reason
            });
        }
    }
}
