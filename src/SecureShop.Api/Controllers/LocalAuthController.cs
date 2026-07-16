using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SecureShop.Api.Contracts.Requests;
using SecureShop.Api.Security.Identity;

namespace SecureShop.Api.Controllers;

[ApiController]
[Route("api/auth/local")]
[AllowAnonymous]
public sealed class LocalAuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;

    public LocalAuthController(
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn)
    {
        _users = users;
        _signIn = signIn;
    }

    [HttpPost("login"), EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await _users.FindByEmailAsync(
            request.Email.Trim());

        if (user is null || !user.IsActive)
        {
            return Unauthorized(
                CreateProblem("E-posta veya parola geçersiz."));
        }

        var result = await _signIn.PasswordSignInAsync(
            user,
            request.Password,
            isPersistent: false,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            return NoContent();
        }

        if (result.IsLockedOut)
        {
            return StatusCode(
                StatusCodes.Status423Locked,
                CreateProblem("Hesap geçici olarak kilitlendi."));
        }

        if (result.RequiresTwoFactor)
        {
            return Conflict(
                CreateProblem("İki faktörlü doğrulama gerekli."));
        }

        if (result.IsNotAllowed)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                CreateProblem(
                    "Hesabın giriş koşulları karşılanmıyor."));
        }

        return Unauthorized(
            CreateProblem("E-posta veya parola geçersiz."));
    }

    private static ProblemDetails CreateProblem(string detail)
    {
        return new ProblemDetails
        {
            Detail = detail
        };
    }
}
