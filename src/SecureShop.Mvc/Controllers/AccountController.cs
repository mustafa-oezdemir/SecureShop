using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureShop.Mvc.Models.ViewModels;
using SecureShop.Mvc.Security;
using SecureShop.Mvc.Services.Interfaces;

namespace SecureShop.Mvc.Controllers;

[Route("account")]
public sealed class AccountController : Controller
{
    private readonly IAuthApiService _authApiService;

    public AccountController(IAuthApiService authApiService)
    {
        _authApiService = authApiService;
    }

    [AllowAnonymous]
    [HttpGet("login")]
    public IActionResult Login([FromQuery] string? returnUrl)
    {
        var safeReturnUrl = GetSafeReturnUrl(returnUrl);

        if (User.Identity?.IsAuthenticated == true)
        {
            return safeReturnUrl is null
                ? RedirectToAction(nameof(Session))
                : LocalRedirect(safeReturnUrl);
        }

        return View(new LoginViewModel
        {
            ReturnUrl = safeReturnUrl
        });
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        LoginViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _authApiService.LoginAsync(
            model.Email,
            model.Password,
            cancellationToken);

        model.Password = string.Empty;

        if (!result.Succeeded || result.AuthenticationCookie is null)
        {
            ModelState.AddModelError(
                string.Empty,
                result.ErrorMessage ?? "Giriş başarısız.");

            return View(model);
        }

        Response.Headers.Append("Set-Cookie",result.AuthenticationCookie);

        var safeReturnUrl = GetSafeReturnUrl(model.ReturnUrl);

        return safeReturnUrl is null
            ? RedirectToAction(nameof(Session))
            : LocalRedirect(safeReturnUrl);
    }

    [AllowAnonymous]
    [HttpGet("session")]
    public async Task<IActionResult> Session(
        CancellationToken cancellationToken)
    {
        var apiResponse =
            await _authApiService.GetSessionAsync(cancellationToken);

        var viewModel = new AuthSessionViewModel
        {
            MvcIsAuthenticated =
                User.Identity?.IsAuthenticated == true,
            MvcUserName = User.Identity?.Name,
            ApiStatusCode = (int)apiResponse.StatusCode,
            ApiSession = apiResponse.Data,
            ErrorMessage = apiResponse.ErrorMessage
        };

        return View(viewModel);
    }

    [AllowAnonymous]
    [HttpGet("forbidden")]
    public IActionResult Forbidden([FromQuery] string? returnUrl)
    {
        ViewData["ReturnUrl"] = GetSafeReturnUrl(returnUrl);
        return View();
    }

    [Authorize]
    [HttpPost("switch-account")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SwitchAccount(
        string? returnUrl)
    {
        await HttpContext.SignOutAsync(
            SharedCookieAuthenticationDefaults.AuthenticationScheme);

        return RedirectToAction(
            nameof(Login),
            new
            {
                returnUrl = GetSafeReturnUrl(returnUrl)
            });
    }

    [Authorize]
    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(
            SharedCookieAuthenticationDefaults.AuthenticationScheme);

        return RedirectToAction(nameof(Session));
    }

    private string? GetSafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl)
        && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : null;
}
