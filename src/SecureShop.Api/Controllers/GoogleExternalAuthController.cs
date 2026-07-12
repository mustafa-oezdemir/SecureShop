using Google.Apis.Auth.AspNetCore3;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SecureShop.Api.Contracts.Responses;
using SecureShop.Api.Features.Auth.External;
using SecureShop.Api.Security.Identity;

namespace SecureShop.Api.Controllers;

[ApiController]
[Route("api/auth/external/google")]
[AllowAnonymous]
[ResponseCache(
    Duration = 0,
    Location = ResponseCacheLocation.None,
    NoStore = true)]
public sealed class GoogleExternalAuthController
    : ControllerBase
{
    private const int Status423Locked = 423;

    private const string CallbackUrl =
        "/api/auth/external/google/callback";

    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IGoogleExternalLoginService
        _googleExternalLoginService;
    private readonly ILogger<GoogleExternalAuthController> _logger;

    public GoogleExternalAuthController(
        SignInManager<ApplicationUser> signInManager,
        IGoogleExternalLoginService googleExternalLoginService,
        ILogger<GoogleExternalAuthController> logger)
    {
        ArgumentNullException.ThrowIfNull(signInManager);
        ArgumentNullException.ThrowIfNull(googleExternalLoginService);
        ArgumentNullException.ThrowIfNull(logger);

        _signInManager = signInManager;
        _googleExternalLoginService =
            googleExternalLoginService;
        _logger = logger;
    }

    [HttpGet("start")]
    public IActionResult Start()
    {
        SetNoStoreHeaders();

        if (User.Identity?.IsAuthenticated == true)
        {
            return Conflict(
                CreateProblemDetails(
                    StatusCodes.Status409Conflict,
                    "Kullanici zaten oturum acmis durumda.",
                    "Google ile farkli bir hesaba gecmeden once mevcut oturum kapatilmalidir.",
                    "already_authenticated"));
        }

        var properties =
            _signInManager
                .ConfigureExternalAuthenticationProperties(
                    GoogleOpenIdConnectDefaults
                        .AuthenticationScheme,
                    CallbackUrl);

        return Challenge(
            properties,
            GoogleOpenIdConnectDefaults
                .AuthenticationScheme);
    }

    [HttpGet("callback")]
    [ProducesResponseType<GoogleExternalLoginResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(
        StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GoogleExternalLoginResponse>>
        Callback(
            [FromQuery] string? remoteError,
            CancellationToken cancellationToken)
    {
        SetNoStoreHeaders();

        if (!string.IsNullOrWhiteSpace(remoteError))
        {
            _logger.LogWarning(
                "Google external authentication saglayici hatasiyla sonlandi.");

            await ClearExternalCookieAsync();

            return BadRequest(
                CreateProblemDetails(
                    StatusCodes.Status400BadRequest,
                    "Google authentication tamamlanamadi.",
                    "Harici authentication saglayicisi islemi reddetti.",
                    "external_provider_error"));
        }

        var externalLoginInfo =
            await _signInManager
                .GetExternalLoginInfoAsync();

        if (externalLoginInfo is null)
        {
            await ClearExternalCookieAsync();

            return BadRequest(
                CreateProblemDetails(
                    StatusCodes.Status400BadRequest,
                    "Google authentication bilgisi alinamadi.",
                    "Authentication islemini yeniden baslatin.",
                    "external_login_info_missing"));
        }

        var result =
            await _googleExternalLoginService.CompleteAsync(
                externalLoginInfo,
                cancellationToken);

        await ClearExternalCookieAsync();

        return result.Status switch
        {
            GoogleExternalLoginStatus.Succeeded =>
                Ok(
                    new GoogleExternalLoginResponse(
                        Status: "authenticated",
                        Email: result.Email
                            ?? string.Empty,
                        DisplayName: result.DisplayName
                            ?? string.Empty,
                        Roles: result.Roles
                            ?? Array.Empty<string>())),

            GoogleExternalLoginStatus.RequiresTwoFactor =>
                StatusCode(
                    StatusCodes.Status409Conflict,
                    CreateProblemDetails(
                        StatusCodes.Status409Conflict,
                        "Iki faktorlu dogrulama gerekli.",
                        "Google dogrulamasi tamamlandi ancak SecureShop TOTP dogrulamasi gereklidir.",
                        "requires_two_factor")),

            GoogleExternalLoginStatus.LockedOut =>
                StatusCode(
                    Status423Locked,
                    CreateProblemDetails(
                        Status423Locked,
                        "Hesap gecici olarak kilitlendi.",
                        "Daha sonra yeniden deneyin.",
                        "account_locked")),

            GoogleExternalLoginStatus.NotAllowed =>
                StatusCode(
                    StatusCodes.Status403Forbidden,
                    CreateProblemDetails(
                        StatusCodes.Status403Forbidden,
                        "Oturum acmaya izin verilmiyor.",
                        "Hesabin e-posta onayi veya guvenlik durumu kontrol edilmelidir.",
                        "sign_in_not_allowed")),

            GoogleExternalLoginStatus.Inactive =>
                StatusCode(
                    StatusCodes.Status403Forbidden,
                    CreateProblemDetails(
                        StatusCodes.Status403Forbidden,
                        "Hesap aktif degil.",
                        "Bu SecureShop hesabi devre disi birakilmistir.",
                        "account_inactive")),

            GoogleExternalLoginStatus.EmailNotAvailable =>
                BadRequest(
                    CreateProblemDetails(
                        StatusCodes.Status400BadRequest,
                        "Google e-posta bilgisi alinamadi.",
                        "Google hesabi kullanilabilir bir e-posta claim'i saglamadi.",
                        "email_not_available")),

            GoogleExternalLoginStatus.EmailNotVerified =>
                StatusCode(
                    StatusCodes.Status403Forbidden,
                    CreateProblemDetails(
                        StatusCodes.Status403Forbidden,
                        "Google e-posta adresi dogrulanmamis.",
                        "Yalnizca dogrulanmis Google e-posta adresleri kabul edilir.",
                        "email_not_verified")),

            GoogleExternalLoginStatus.EmailAlreadyRegistered =>
                Conflict(
                    CreateProblemDetails(
                        StatusCodes.Status409Conflict,
                        "Bu e-posta adresi zaten kayitli.",
                        "Guvenlik nedeniyle mevcut yerel hesap otomatik olarak Google hesabina baglanmaz.",
                        "email_already_registered")),

            _ =>
                BadRequest(
                    CreateProblemDetails(
                        StatusCodes.Status400BadRequest,
                        "Google ile oturum acilamadi.",
                        "Authentication islemini yeniden baslatin.",
                        "external_login_failed"))
        };
    }

    private async Task ClearExternalCookieAsync()
    {
        await HttpContext.SignOutAsync(
            IdentityConstants.ExternalScheme);
    }

    private void SetNoStoreHeaders()
    {
        Response.Headers.CacheControl =
            "no-store, no-cache, max-age=0";

        Response.Headers.Pragma =
            "no-cache";
    }

    private static ProblemDetails CreateProblemDetails(
        int status,
        string title,
        string detail,
        string code)
    {
        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail
        };

        problemDetails.Extensions["code"] = code;

        return problemDetails;
    }
}
