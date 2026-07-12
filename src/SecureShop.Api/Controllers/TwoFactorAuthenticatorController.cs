using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SecureShop.Api.Contracts.Requests;
using SecureShop.Api.Contracts.Responses;
using SecureShop.Api.Features.Auth.TwoFactor;
using SecureShop.Api.Security.Identity;
using SecureShop.Api.Security.Policies;

namespace SecureShop.Api.Controllers;

[ApiController]
[Route("api/auth/two-factor/authenticator")]
[Authorize(Policy = AppPolicies.StaffOnly)]
[ResponseCache(
    Duration = 0,
    Location = ResponseCacheLocation.None,
    NoStore = true)]
public sealed class TwoFactorAuthenticatorController
    : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuthenticatorEnrollmentService
        _authenticatorEnrollmentService;

    public TwoFactorAuthenticatorController(
        UserManager<ApplicationUser> userManager,
        IAuthenticatorEnrollmentService
            authenticatorEnrollmentService)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(authenticatorEnrollmentService);

        _userManager = userManager;
        _authenticatorEnrollmentService =
            authenticatorEnrollmentService;
    }

    [HttpGet("setup")]
    [ProducesResponseType<AuthenticatorSetupResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(
        StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthenticatorSetupResponse>>
        GetSetup(
            CancellationToken cancellationToken)
    {
        SetNoStoreHeaders();

        var user = await _userManager.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized();
        }

        if (!user.IsActive)
        {
            return Forbid();
        }

        var isTwoFactorEnabled =
            await _userManager.GetTwoFactorEnabledAsync(user);

        if (isTwoFactorEnabled)
        {
            return Conflict(
                CreateProblemDetails(
                    StatusCodes.Status409Conflict,
                    "Iki faktorlu kimlik dogrulama zaten etkin.",
                    "Mevcut authenticator anahtari yeniden gosterilmez."));
        }

        var response =
            await _authenticatorEnrollmentService
                .CreateSetupAsync(
                    user,
                    cancellationToken);

        return Ok(response);
    }

    [HttpPost("enable")]
    [ProducesResponseType<EnableAuthenticatorResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(
        StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EnableAuthenticatorResponse>>
        Enable(
            [FromBody] EnableAuthenticatorRequest request,
            CancellationToken cancellationToken)
    {
        SetNoStoreHeaders();

        var user = await _userManager.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized();
        }

        if (!user.IsActive)
        {
            return Forbid();
        }

        var isTwoFactorEnabled =
            await _userManager.GetTwoFactorEnabledAsync(user);

        if (isTwoFactorEnabled)
        {
            return Conflict(
                CreateProblemDetails(
                    StatusCodes.Status409Conflict,
                    "Iki faktorlu kimlik dogrulama zaten etkin.",
                    "Authenticator kurulumu ikinci kez etkinlestirilemez."));
        }

        var response =
            await _authenticatorEnrollmentService.EnableAsync(
                user,
                request.Code,
                cancellationToken);

        if (response is null)
        {
            ModelState.AddModelError(
                nameof(request.Code),
                "Authenticator dogrulama kodu gecersiz.");

            return ValidationProblem(ModelState);
        }

        return Ok(response);
    }

    private void SetNoStoreHeaders()
    {
        Response.Headers.CacheControl =
            "no-store, no-cache, max-age=0";

        Response.Headers.Pragma = "no-cache";
    }

    private static ProblemDetails CreateProblemDetails(
        int status,
        string title,
        string detail)
    {
        return new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail
        };
    }
}
