using Microsoft.AspNetCore.Mvc;
using SecureShop.Api.Contracts.Responses;

namespace SecureShop.Api.Controllers;

[ApiController]
[Route("api/system")]
public sealed class SystemController : ControllerBase
{
    [HttpGet("status")]
    [ProducesResponseType<SystemStatusResponse>(StatusCodes.Status200OK)]
    public ActionResult<SystemStatusResponse> GetStatus()
    {
        var response = new SystemStatusResponse(
            Application: "SecureShop.Api",
            Status: "Running",
            UtcTimestamp: DateTimeOffset.UtcNow);

        return Ok(response);
    }
}