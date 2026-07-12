using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SecureShop.Api.Contracts.Responses;
using SecureShop.Api.Data;

namespace SecureShop.Api.Controllers;

[ApiController]
[Route("api/system")]
[AllowAnonymous]
public sealed class SystemController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<SystemController> _logger;

    public SystemController(
        AppDbContext dbContext,
        ILogger<SystemController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet("status")]
    [ProducesResponseType<SystemStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<SystemStatusResponse>(
        StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<SystemStatusResponse>> GetStatus(
        CancellationToken cancellationToken)
    {
        var databaseIsAvailable = false;

        try
        {
            databaseIsAvailable =
                await _dbContext.Database.CanConnectAsync(
                    cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                "Database connectivity check failed. Exception type: {ExceptionType}",
                exception.GetType().Name);
        }

        var response = new SystemStatusResponse(
            Application: "SecureShop.Api",
            Status: databaseIsAvailable
                ? "Running"
                : "Degraded",
            Database: databaseIsAvailable
                ? "Connected"
                : "Unavailable",
            UtcTimestamp: DateTimeOffset.UtcNow);

        if (!databaseIsAvailable)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                response);
        }

        return Ok(response);
    }
}
