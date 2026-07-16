using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SecureShop.Api.Contracts.Responses;
using SecureShop.Api.Security.Identity;

namespace SecureShop.Api.Controllers;

[ApiController]
[Route("api/auth/session")]
[Authorize]
[ResponseCache(
    Duration = 0,
    Location = ResponseCacheLocation.None,
    NoStore = true)]
public sealed class AuthSessionController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;

    public AuthSessionController(
        UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    [HttpGet]
    [ProducesResponseType<AuthSessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuthSessionResponse>> GetSession(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await _userManager.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized();
        }

        if (!user.IsActive)
        {
            return Forbid();
        }

        var roles = await _userManager.GetRolesAsync(user);
        var twoFactorEnabled =
            await _userManager.GetTwoFactorEnabledAsync(user);

        cancellationToken.ThrowIfCancellationRequested();

        var response = new AuthSessionResponse(
            UserId: user.Id,
            Email: user.Email ?? string.Empty,
            DisplayName: $"{user.FirstName} {user.LastName}".Trim(),
            Roles: roles.ToArray(),
            TwoFactorEnabled: twoFactorEnabled);

        return Ok(response);
    }
}
