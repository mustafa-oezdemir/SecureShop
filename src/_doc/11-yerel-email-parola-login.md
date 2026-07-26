# Yerel E-posta/Parola Login

## Amaç

MVC antiforgery korumalı formdan parolayı HTTPS ile API'ye iletir. Identity doğrulaması, lockout ve cookie üretimi yalnızca API'de yapılır. MVC API'nin ürettiği `__Host-SecureShop.Auth` Set-Cookie header'ını kontrollü biçimde tarayıcıya aktarır.

## Güvenlik

- Login API: 5 istek/dakika/IP rate limit.
- Identity: 5 başarısız deneme sonrası 15 dakika lockout.
- Genel hatalı credential mesajı kullanıcı keşfini azaltır.
- Parola ve cookie loglanmaz.
- MVC POST antiforgery ile korunur.
- Cookie HttpOnly, Secure, SameSite=Lax ve Path=/ özelliklerini API'den alır.

## Akış

```text
Razor Login → MVC POST + antiforgery → AuthApiService → HTTPS API
→ PasswordSignInAsync → Identity/SQL Server → Set-Cookie → MVC response → Tarayıcı
```

## Rotalar

- MVC: `GET/POST /account/login`
- API: `POST /api/auth/local/login`

## Test

```powershell
dotnet build .\SecureShop.sln -c Release
dotnet watch run --project .\src\SecureShop.Api\SecureShop.Api.csproj --launch-profile https
dotnet watch run --project .\src\SecureShop.Mvc\SecureShop.Mvc.csproj --launch-profile https
```

Tarayıcı: `https://localhost:7002/account/login`

Build sonucu: 0 uyarı, 0 hata.

# Dosya bazında eksiksiz kodlar

## `src/SecureShop.Api/Contracts/Requests/LoginRequest.cs`

Uzantı: `.cs`

```csharp
using System.ComponentModel.DataAnnotations;
namespace SecureShop.Api.Contracts.Requests;
public sealed class LoginRequest
{
    [Required, EmailAddress, StringLength(256)] public string Email { get; init; } = string.Empty;
    [Required, StringLength(200, MinimumLength = 1)] public string Password { get; init; } = string.Empty;
}
```

## `src/SecureShop.Api/Controllers/LocalAuthController.cs`

Uzantı: `.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SecureShop.Api.Contracts.Requests;
using SecureShop.Api.Security.Identity;
namespace SecureShop.Api.Controllers;
[ApiController, Route("api/auth/local"), AllowAnonymous]
public sealed class LocalAuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    public LocalAuthController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn) { _users=users; _signIn=signIn; }
    [HttpPost("login"), EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user=await _users.FindByEmailAsync(request.Email.Trim());
        if(user is null || !user.IsActive) return Unauthorized(Problem("E-posta veya parola geçersiz."));
        var result=await _signIn.PasswordSignInAsync(user,request.Password,false,true);
        if(result.Succeeded) return NoContent();
        if(result.IsLockedOut) return StatusCode(423,Problem("Hesap geçici olarak kilitlendi."));
        if(result.RequiresTwoFactor) return Conflict(Problem("İki faktörlü doğrulama gerekli."));
        return Unauthorized(Problem("E-posta veya parola geçersiz."));
    }
    private static ProblemDetails Problem(string detail)=>new(){Detail=detail};
}
```

## `src/SecureShop.Api/Program.cs`

Uzantı: `.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using SecureShop.Api.Configuration;
using SecureShop.Api.Data;
using SecureShop.Api.Data.Seed;
using SecureShop.Api.Domain.Constants;
using SecureShop.Api.Features.Auth.External;
using SecureShop.Api.Features.Auth.TwoFactor;
using SecureShop.Api.Features.Products;
using SecureShop.Api.Security.Identity;
using SecureShop.Api.Security.Policies;
using SecureShop.Api.Services.Email;

var builder = WebApplication.CreateBuilder(args);

DotEnvConfiguration.AddMissingFromDotEnv(
    builder.Configuration,
    builder.Environment.ContentRootPath);

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' was not found.");

builder.Services
    .AddSecureShopSharedCookieDataProtection(
        builder.Configuration,
        builder.Environment);

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(connectionString);
});

builder.Services.AddSecureShopIdentity();

builder.Services.AddSecureShopGoogleAuthentication(
    builder.Configuration);

builder.Services.AddSecureShopEmail(
    builder.Configuration);

builder.Services.AddSecureShopTwoFactor(
    builder.Configuration);

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy(
        AppPolicies.AdminOnly,
        policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireRole(AppRoles.Admin);
        });

    options.AddPolicy(
        AppPolicies.StaffOnly,
        policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireRole(
                AppRoles.Admin,
                AppRoles.Employee);
        });

    options.AddPolicy(
        AppPolicies.CustomerOnly,
        policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireRole(AppRoles.Kunde);
        });
});

builder.Services.AddScoped<IdentitySeeder>();
builder.Services.AddScoped<IProductService, ProductService>();

builder.Services.AddControllers();
builder.Services.AddRateLimiter(options => options.AddPolicy("login", context =>
    RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();

    var dbContext =
        scope.ServiceProvider.GetRequiredService<AppDbContext>();

    await dbContext.Database.MigrateAsync();

    var identitySeeder =
        scope.ServiceProvider.GetRequiredService<IdentitySeeder>();

    await identitySeeder.SeedAsync();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();
app.UseRateLimiter();

app.UseCookiePolicy();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await app.RunAsync();
```

## `src/SecureShop.Mvc/Models/ViewModels/LoginViewModel.cs`

Uzantı: `.cs`

```csharp
using System.ComponentModel.DataAnnotations;
namespace SecureShop.Mvc.Models.ViewModels;
public sealed class LoginViewModel
{
    [Required, EmailAddress, Display(Name="E-posta")] public string Email { get; set; }=string.Empty;
    [Required, DataType(DataType.Password), Display(Name="Parola")] public string Password { get; set; }=string.Empty;
}
```

## `src/SecureShop.Mvc/Models/Responses/LoginApiResult.cs`

Uzantı: `.cs`

```csharp
using System.Net;
namespace SecureShop.Mvc.Models.Responses;
public sealed record LoginApiResult(bool Succeeded,HttpStatusCode StatusCode,string? AuthenticationCookie,string? ErrorMessage);
```

## `src/SecureShop.Mvc/Services/Interfaces/IAuthApiService.cs`

Uzantı: `.cs`

```csharp
using SecureShop.Mvc.Http;
using SecureShop.Mvc.Models.Responses;

namespace SecureShop.Mvc.Services.Interfaces;

public interface IAuthApiService
{
    Task<LoginApiResult> LoginAsync(string email,string password,CancellationToken cancellationToken=default);
    Task<ApiResponse<AuthSessionResponse>> GetSessionAsync(
        CancellationToken cancellationToken = default);
}
```

## `src/SecureShop.Mvc/Services/Api/AuthApiService.cs`

Uzantı: `.cs`

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SecureShop.Mvc.Http;
using SecureShop.Mvc.Models.Responses;
using SecureShop.Mvc.Services.Interfaces;

namespace SecureShop.Mvc.Services.Api;

public sealed class AuthApiService : IAuthApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AuthApiService> _logger;

    public AuthApiService(
        HttpClient httpClient,
        ILogger<AuthApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<LoginApiResult> LoginAsync(string email,string password,CancellationToken cancellationToken=default)
    {
        try
        {
            using var response=await _httpClient.PostAsJsonAsync("api/auth/local/login",new { email,password },cancellationToken);
            string? cookie=null;
            if(response.Headers.TryGetValues("Set-Cookie",out var values))
                cookie=values.FirstOrDefault(value=>value.StartsWith("__Host-SecureShop.Auth=",StringComparison.Ordinal));
            if(response.IsSuccessStatusCode && cookie is not null) return new(true,response.StatusCode,cookie,null);
            var message=response.StatusCode switch { (HttpStatusCode)423=>"Hesap geçici olarak kilitlendi.",HttpStatusCode.Conflict=>"İki faktörlü doğrulama gerekli.",HttpStatusCode.TooManyRequests=>"Çok fazla deneme yapıldı.",_=>"E-posta veya parola geçersiz."};
            return new(false,response.StatusCode,null,message);
        }
        catch(HttpRequestException exception){_logger.LogWarning(exception,"Yerel login API isteği tamamlanamadı.");return new(false,HttpStatusCode.ServiceUnavailable,null,"API hizmetine ulaşılamıyor.");}
    }

    public async Task<ApiResponse<AuthSessionResponse>> GetSessionAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(
                "api/auth/session",
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return ApiResponse<AuthSessionResponse>.Failure(
                    response.StatusCode,
                    "API oturumu bulunamadı.");
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return ApiResponse<AuthSessionResponse>.Failure(
                    response.StatusCode,
                    "API bu hesap için erişimi reddetti.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return ApiResponse<AuthSessionResponse>.Failure(
                    response.StatusCode,
                    "API oturum bilgisi alınamadı.");
            }

            var session =
                await response.Content.ReadFromJsonAsync<AuthSessionResponse>(
                    cancellationToken: cancellationToken);

            if (session is null)
            {
                return ApiResponse<AuthSessionResponse>.Failure(
                    HttpStatusCode.BadGateway,
                    "API geçerli bir oturum response'u döndürmedi.");
            }

            return ApiResponse<AuthSessionResponse>.Success(
                response.StatusCode,
                session);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                exception,
                "SecureShop API oturum isteği tamamlanamadı.");

            return ApiResponse<AuthSessionResponse>.Failure(
                HttpStatusCode.ServiceUnavailable,
                "SecureShop API hizmetine ulaşılamıyor.");
        }
        catch (JsonException exception)
        {
            _logger.LogError(
                exception,
                "SecureShop API oturum response'u okunamadı.");

            return ApiResponse<AuthSessionResponse>.Failure(
                HttpStatusCode.BadGateway,
                "API oturum response formatı geçersiz.");
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return ApiResponse<AuthSessionResponse>.Failure(
                HttpStatusCode.GatewayTimeout,
                "SecureShop API isteği zaman aşımına uğradı.");
        }
    }
}
```

## `src/SecureShop.Mvc/Controllers/AccountController.cs`

Uzantı: `.cs`

```csharp
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
    [AllowAnonymous, HttpGet("login")]
    public IActionResult Login()=>User.Identity?.IsAuthenticated==true?RedirectToAction(nameof(Session)):View(new LoginViewModel());

    [AllowAnonymous, HttpPost("login"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model,CancellationToken cancellationToken)
    {
        if(!ModelState.IsValid)return View(model);
        var result=await _authApiService.LoginAsync(model.Email,model.Password,cancellationToken);
        model.Password=string.Empty;
        if(!result.Succeeded||result.AuthenticationCookie is null){ModelState.AddModelError(string.Empty,result.ErrorMessage??"Giriş başarısız.");return View(model);}
        Response.Headers.Append("Set-Cookie",result.AuthenticationCookie);
        return RedirectToAction(nameof(Session));
    }
    private readonly IAuthApiService _authApiService;

    public AccountController(IAuthApiService authApiService)
    {
        _authApiService = authApiService;
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

    [Authorize]
    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(
            SharedCookieAuthenticationDefaults.AuthenticationScheme);

        return RedirectToAction(nameof(Session));
    }
}
```

## `src/SecureShop.Mvc/Views/Account/Login.cshtml`

Uzantı: `.cshtml`

```cshtml
@model SecureShop.Mvc.Models.ViewModels.LoginViewModel
@{ ViewData["Title"]="Giriş"; }
<div class="row justify-content-center"><div class="col-md-5"><h1>Yerel Giriş</h1>
<form asp-action="Login" method="post">
<div asp-validation-summary="ModelOnly" class="text-danger"></div>
<div class="mb-3"><label asp-for="Email" class="form-label"></label><input asp-for="Email" class="form-control" autocomplete="username"/><span asp-validation-for="Email" class="text-danger"></span></div>
<div class="mb-3"><label asp-for="Password" class="form-label"></label><input asp-for="Password" class="form-control" autocomplete="current-password"/><span asp-validation-for="Password" class="text-danger"></span></div>
<button class="btn btn-primary" type="submit">Giriş yap</button></form></div></div>
@section Scripts { <partial name="_ValidationScriptsPartial" /> }
```

