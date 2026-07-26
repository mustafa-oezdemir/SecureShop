# SecureShop Cookie Authentication ve MVC–API Entegrasyonu

## 1. Adımın amacı

Bu adımda `SecureShop.Api` tarafından ASP.NET Core Identity ile oluşturulan
authentication cookie'sinin `SecureShop.Mvc` tarafından okunması ve MVC'nin
sunucu tarafındaki API çağrılarına güvenli biçimde aktarılması sağlanmıştır.

Uygulanan mimari:

```text
Tarayıcı
   ↓ ortak şifreli authentication cookie
SecureShop.Mvc
   ↓ yalnızca authentication cookie'sini aktaran HttpClient
SecureShop.Api
   ↓ ASP.NET Core Identity / AppDbContext
SQL Server
```

Bu adım login formu, yerel kullanıcı kayıt endpoint'i veya parola doğrulama
ekranı oluşturmaz. Cookie'nin güvenilir üreticisi API tarafındaki ASP.NET Core
Identity'dir. MVC parola doğrulamaz, kullanıcı claim'i üretmez ve SQL Server'a
doğrudan erişmez.

## 2. Etkilenen uygulamalar

```text
SecureShop.Api : Ortak cookie, Data Protection ve session endpoint'i
SecureShop.Mvc : Cookie authentication, API istemcisi ve oturum ekranı
SQL Server     : Şema değişmedi
```

Bu adımda yeni NuGet paketi ve Entity Framework Core migration'ı gerekmez.

## 3. Mimari ve güvenlik kararları

### 3.1. Ortak authentication cookie

API ve MVC aşağıdaki değerleri aynı kullanır:

```text
Authentication scheme       : Identity.Application
Cookie adı                  : __Host-SecureShop.Auth
Data Protection app name    : SecureShop.SharedCookie.v1
Cookie Path                 : /
Cookie Domain               : tanımlı değil
Cookie HttpOnly             : true
Cookie Secure               : always
Cookie SameSite             : Lax
Cookie ömrü                 : 20 dakika
Sliding expiration          : true
```

`__Host-` cookie öneki kullanıldığı için `Secure` zorunludur, `Path=/`
olmalıdır ve `Domain` tanımlanmamalıdır.

### 3.2. Ortak Data Protection key ring

Development ortamındaki varsayılan key ring dizini:

```text
%LOCALAPPDATA%\SecureShop\DataProtection-Keys
```

Windows üzerinde key ring anahtarları DPAPI ile mevcut Windows kullanıcı
hesabına bağlı olarak korunur. API ve MVC development sırasında aynı Windows
kullanıcısıyla çalıştırılmalıdır.

Production ortamında aşağıdaki yapılandırma zorunludur:

```text
Authentication:SharedCookie:KeyRingPath
```

Ortam değişkeni karşılığı:

```text
Authentication__SharedCookie__KeyRingPath
```

Çok sunuculu veya Linux production yerleşiminde Redis, Azure Blob, X.509
sertifikası veya benzeri ortak ve güvenli bir key store seçilmelidir.

### 3.3. MVC'nin API'ye cookie aktarması

Tarayıcının MVC'ye gönderdiği cookie, MVC tarafından oluşturulan sunucu tarafı
`HttpClient` isteğine otomatik eklenmez. `AuthenticationDelegatingHandler`
yalnızca `__Host-SecureShop.Auth` cookie'sini seçerek API isteğine ekler.

MVC'ye ait antiforgery veya başka cookie'ler API'ye gönderilmez.

`SocketsHttpHandler.UseCookies = false` kullanılır. Böylece
`IHttpClientFactory` tarafından havuzlanan handler'ın bir kullanıcının
authentication cookie'sini saklayıp başka bir kullanıcı isteğinde yeniden
kullanması engellenir.

### 3.4. 401 ve 403 davranışı

API authentication cookie yapılandırmasında:

```text
Unauthenticated : 401 Unauthorized
Unauthorized    : 403 Forbidden
```

API login sayfasına `302` redirect döndürmez. MVC bu durum kodlarını kontrollü
bir `ApiResponse<T>` sonucu olarak işler.

### 3.5. Logout ve CSRF koruması

MVC logout action'ı yalnızca `POST` kabul eder ve
`ValidateAntiForgeryToken` ile korunur. İşlem ortak cookie'yi
`SignOutAsync("Identity.Application")` çağrısıyla tarayıcıdan siler.

## 4. Bu adımda yapılan işlemler

1. API ve MVC için ortak cookie sabitleri oluşturuldu.
2. İki uygulamada aynı Data Protection key ring yapılandırıldı.
3. API Identity cookie adı `__Host-SecureShop.Auth` olarak değiştirildi.
4. API'ye `GET /api/auth/session` endpoint'i eklendi.
5. Session response'un cache'lenmesi engellendi.
6. MVC'ye cookie authentication eklendi.
7. MVC'ye yalnızca authentication cookie'sini aktaran delegating handler eklendi.
8. MVC API adresi `appsettings.json` üzerinden yapılandırıldı.
9. `IAuthApiService` typed `HttpClient` olarak kaydedildi.
10. `401`, `403`, bağlantı, zaman aşımı ve JSON hataları kontrollü işlendi.
11. MVC session kontrol ekranı oluşturuldu.
12. MVC logout action'ı POST ve antiforgery korumasıyla eklendi.
13. Middleware sırası cookie authentication için düzenlendi.
14. Mevcut `.env` yüklemesi ve .NET 10 static-assets yapısı korundu.

## 5. Dosya yolları ve uzantıları

| No | Durum | Uygulama | Dosya yolu | Uzantı |
|---:|---|---|---|---|
| 1 | Yeni | API | `src/SecureShop.Api/Security/Identity/SharedCookieAuthenticationDefaults.cs` | `.cs` |
| 2 | Yeni | API | `src/SecureShop.Api/Security/Identity/SharedCookieDataProtectionExtensions.cs` | `.cs` |
| 3 | Yeni | API | `src/SecureShop.Api/Contracts/Responses/AuthSessionResponse.cs` | `.cs` |
| 4 | Yeni | API | `src/SecureShop.Api/Controllers/AuthSessionController.cs` | `.cs` |
| 5 | Değişti | API | `src/SecureShop.Api/Security/Identity/IdentityServiceCollectionExtensions.cs` | `.cs` |
| 6 | Değişti | API | `src/SecureShop.Api/Program.cs` | `.cs` |
| 7 | Yeni | MVC | `src/SecureShop.Mvc/Security/SharedCookieAuthenticationDefaults.cs` | `.cs` |
| 8 | Yeni | MVC | `src/SecureShop.Mvc/Security/SharedCookieDataProtectionExtensions.cs` | `.cs` |
| 9 | Yeni | MVC | `src/SecureShop.Mvc/Http/AuthenticationDelegatingHandler.cs` | `.cs` |
| 10 | Yeni | MVC | `src/SecureShop.Mvc/Http/ApiResponse.cs` | `.cs` |
| 11 | Yeni | MVC | `src/SecureShop.Mvc/Services/Api/ApiSettings.cs` | `.cs` |
| 12 | Yeni | MVC | `src/SecureShop.Mvc/Models/Responses/AuthSessionResponse.cs` | `.cs` |
| 13 | Yeni | MVC | `src/SecureShop.Mvc/Services/Interfaces/IAuthApiService.cs` | `.cs` |
| 14 | Yeni | MVC | `src/SecureShop.Mvc/Services/Api/AuthApiService.cs` | `.cs` |
| 15 | Yeni | MVC | `src/SecureShop.Mvc/Models/ViewModels/AuthSessionViewModel.cs` | `.cs` |
| 16 | Yeni | MVC | `src/SecureShop.Mvc/Controllers/AccountController.cs` | `.cs` |
| 17 | Yeni | MVC | `src/SecureShop.Mvc/Views/Account/Session.cshtml` | `.cshtml` |
| 18 | Değişti | MVC | `src/SecureShop.Mvc/Program.cs` | `.cs` |
| 19 | Değişti | MVC | `src/SecureShop.Mvc/appsettings.json` | `.json` |

## 6. CLI komutları

Çalışma dizini:

```text
D:\Code\ASP.NET\SecureShop
```

Restore ve build:

```powershell
dotnet restore .\SecureShop.sln
dotnet build .\SecureShop.sln -c Release
```

Migration gerekmediğini doğrulama:

```powershell
dotnet ef migrations has-pending-model-changes --project .\src\SecureShop.Api\SecureShop.Api.csproj --startup-project .\src\SecureShop.Api\SecureShop.Api.csproj -- --environment Development
```

Beklenen çıktı:

```text
No changes have been made to the model since the last migration.
```

API'yi çalıştırma — Terminal 1:

```powershell
dotnet watch run --project .\src\SecureShop.Api\SecureShop.Api.csproj --launch-profile https
```

MVC'yi çalıştırma — Terminal 2:

```powershell
dotnet watch run --project .\src\SecureShop.Mvc\SecureShop.Mvc.csproj --launch-profile https
```

## 7. API–MVC veri akışı

```text
Tarayıcı
    ↓ ortak cookie
GET https://localhost:7002/account/session
    ↓
AccountController
    ↓
IAuthApiService
    ↓
AuthenticationDelegatingHandler
    ↓ yalnızca __Host-SecureShop.Auth
GET https://localhost:7001/api/auth/session
    ↓
AuthSessionController
    ↓
UserManager<ApplicationUser>
    ↓
AppDbContext
    ↓
SQL Server
    ↓
AuthSessionResponse
    ↓
Razor View
```

Logout akışı:

```text
Razor POST form
    ↓ antiforgery doğrulaması
AccountController.Logout
    ↓
SignOutAsync("Identity.Application")
    ↓
Ortak browser cookie silinir
```

## 8. Çalıştırma ve test

Anonim test adresi:

```text
https://localhost:7002/account/session
```

Beklenen anonim sonuç:

```text
MVC Authentication : doğrulanmamış
API HTTP durumu     : 401
```

Google authentication başlangıç adresi:

```text
https://localhost:7001/api/auth/external/google/start
```

Google login tamamlandıktan sonra tekrar açılacak adres:

```text
https://localhost:7002/account/session
```

Beklenen authenticated sonuç:

```text
MVC Authentication : doğrulanmış
API HTTP durumu     : 200
Rol                 : Kunde
```

Bu adım uygulanırken gerçekleştirilen otomatik doğrulama sonuçları:

```text
Release build                 : Başarılı
Uyarı                         : 0
Hata                          : 0
Pending EF model değişikliği  : Yok
Anonim API session            : 401
MVC session sayfası           : 200
MVC üzerinde API sonucu       : 401 olarak kontrollü gösterildi
```

## 9. Yaygın hatalar

### MVC cookie'yi okuyamıyor

1. API ve MVC'nin aynı Windows kullanıcısıyla çalıştığını doğrulayın.
2. Eski `__Host-SecureShop.Api.Auth` cookie'sini tarayıcıdan silin.
3. İki uygulamayı yeniden başlatın.
4. Key ring dizinini kontrol edin:

```powershell
Get-ChildItem "$env:LOCALAPPDATA\SecureShop\DataProtection-Keys"
```

### MVC API'ye ulaşamıyor

API'nin `https://localhost:7001` adresinde çalıştığını kontrol edin. Gerekirse
development HTTPS sertifikasına güvenin:

```powershell
dotnet dev-certs https --trust
```

### MVC authenticated fakat API 401 döndürüyor

- Cookie adı iki uygulamada da `__Host-SecureShop.Auth` olmalıdır.
- Scheme iki uygulamada da `Identity.Application` olmalıdır.
- API ve MVC aynı key ring dizinini kullanmalıdır.
- API base URL'de `localhost` yerine `127.0.0.1` kullanılmamalıdır.
- Program veya authentication ayarı değiştikten sonra iki uygulama yeniden
  başlatılmalıdır.

### Debug build dosya kilidi hatası

Çalışan `dotnet watch` süreçlerini durdurun veya Release çıktısında build alın:

```powershell
dotnet build .\SecureShop.sln -c Release
```

## 10. Tamamlama kontrol listesi

```text
[x] API ve MVC aynı authentication scheme'i kullanıyor
[x] API ve MVC aynı cookie adını kullanıyor
[x] API ve MVC aynı Data Protection application name'i kullanıyor
[x] API ve MVC aynı development key ring dizinini kullanıyor
[x] Cookie HttpOnly ve Secure
[x] Cookie SameSite Lax
[x] Cookie Path /
[x] Cookie Domain tanımlı değil
[x] API session endpoint'i eklendi
[x] API anonim istekte 401 döndürüyor
[x] MVC cookie authentication yapılandırıldı
[x] MVC doğrudan veritabanına erişmiyor
[x] MVC'ye EF Core veya SQL Server paketi eklenmedi
[x] HttpClient UseCookies=false
[x] API BaseUrl appsettings üzerinden okunuyor
[x] Logout POST ve antiforgery korumalı
[x] Release build hatasız
[x] Pending EF model değişikliği yok
[x] Anonim MVC–API entegrasyonu doğrulandı
[ ] Google login sonrası authenticated entegrasyon tarayıcıda doğrulanmalı
[ ] Logout davranışı authenticated oturumla tarayıcıda doğrulanmalı
```

## 11. Resmî kaynaklar

- [ASP.NET Core uygulamaları arasında authentication cookie paylaşımı](https://learn.microsoft.com/en-us/aspnet/core/security/cookie-sharing?view=aspnetcore-10.0)
- [ASP.NET Core Data Protection key storage providers](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-storage-providers?view=aspnetcore-10.0)
- [IHttpClientFactory ile HTTP istekleri](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/http-requests?view=aspnetcore-10.0)
- [.NET 10 API endpoint authentication davranışı](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/api-endpoint-auth?view=aspnetcore-10.0)

## 12. Dosya bazında eksiksiz kodlar

### `src/SecureShop.Api/Security/Identity/SharedCookieAuthenticationDefaults.cs`

Dosya uzantısı: `.cs`

```csharp
namespace SecureShop.Api.Security.Identity;

public static class SharedCookieAuthenticationDefaults
{
    public const string AuthenticationScheme =
        "Identity.Application";

    public const string CookieName =
        "__Host-SecureShop.Auth";

    public const string DataProtectionApplicationName =
        "SecureShop.SharedCookie.v1";

    public const string KeyRingPathConfigurationKey =
        "Authentication:SharedCookie:KeyRingPath";
}
```

### `src/SecureShop.Api/Security/Identity/SharedCookieDataProtectionExtensions.cs`

Dosya uzantısı: `.cs`

```csharp
using Microsoft.AspNetCore.DataProtection;

namespace SecureShop.Api.Security.Identity;

public static class SharedCookieDataProtectionExtensions
{
    public static IServiceCollection AddSecureShopSharedCookieDataProtection(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var keyRingPath = ResolveKeyRingPath(
            configuration,
            environment);

        Directory.CreateDirectory(keyRingPath);

        var dataProtectionBuilder = services
            .AddDataProtection()
            .PersistKeysToFileSystem(
                new DirectoryInfo(keyRingPath))
            .SetApplicationName(
                SharedCookieAuthenticationDefaults
                    .DataProtectionApplicationName);

        if (OperatingSystem.IsWindows())
        {
            dataProtectionBuilder.ProtectKeysWithDpapi();
        }
        else if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Production ortamında ortak Data Protection anahtarları için " +
                "şifreli ve kalıcı bir key store yapılandırılmalıdır.");
        }

        return services;
    }

    private static string ResolveKeyRingPath(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var configuredPath = configuration[
            SharedCookieAuthenticationDefaults
                .KeyRingPathConfigurationKey];

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var expandedPath =
                Environment.ExpandEnvironmentVariables(
                    configuredPath.Trim());

            return Path.IsPathRooted(expandedPath)
                ? Path.GetFullPath(expandedPath)
                : Path.GetFullPath(
                    expandedPath,
                    environment.ContentRootPath);
        }

        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                $"'{SharedCookieAuthenticationDefaults.KeyRingPathConfigurationKey}' " +
                "production ortamında yapılandırılmalıdır.");
        }

        var localApplicationData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            throw new InvalidOperationException(
                "Yerel Data Protection anahtar dizini belirlenemedi.");
        }

        return Path.Combine(
            localApplicationData,
            "SecureShop",
            "DataProtection-Keys");
    }
}
```

### `src/SecureShop.Api/Contracts/Responses/AuthSessionResponse.cs`

Dosya uzantısı: `.cs`

```csharp
namespace SecureShop.Api.Contracts.Responses;

public sealed record AuthSessionResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    string[] Roles,
    bool TwoFactorEnabled);
```

### `src/SecureShop.Api/Controllers/AuthSessionController.cs`

Dosya uzantısı: `.cs`

```csharp
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
```

### `src/SecureShop.Api/Security/Identity/IdentityServiceCollectionExtensions.cs`

Dosya uzantısı: `.cs`

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using SecureShop.Api.Data;
using SecureShop.Api.Security.Identity.Tokens;

namespace SecureShop.Api.Security.Identity;

public static class IdentityServiceCollectionExtensions
{
    private const int PasswordRequiredLength = 12;
    private const int PasswordRequiredUniqueCharacters = 4;
    private const int PasswordHasherIterationCount = 150_000;
    private const int MaximumFailedAccessAttempts = 5;

    private static readonly TimeSpan LockoutDuration =
        TimeSpan.FromMinutes(15);

    private static readonly TimeSpan AuthenticationCookieLifetime =
        TimeSpan.FromMinutes(20);

    private static readonly TimeSpan SecurityStampValidationInterval =
        TimeSpan.FromMinutes(5);

    private static readonly TimeSpan PasswordResetTokenLifetime =
        TimeSpan.FromHours(1);

    public static IServiceCollection AddSecureShopIdentity(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddIdentity<ApplicationUser, ApplicationRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders()
            .AddTokenProvider<
                EmailConfirmationTokenProvider<ApplicationUser>>(
                AppTokenProviders.EmailConfirmation);

        services.AddOptions<
            EmailConfirmationTokenProviderOptions>();

        services.Configure<IdentityOptions>(options =>
        {
            ConfigureClaims(options);
            ConfigurePassword(options);
            ConfigureLockout(options);
            ConfigureSignIn(options);
            ConfigureUser(options);
            ConfigureTokens(options);
        });

        services.Configure<PasswordHasherOptions>(options =>
        {
            options.CompatibilityMode =
                PasswordHasherCompatibilityMode.IdentityV3;

            options.IterationCount =
                PasswordHasherIterationCount;
        });

        services.Configure<SecurityStampValidatorOptions>(options =>
        {
            options.ValidationInterval =
                SecurityStampValidationInterval;
        });

        services.Configure<DataProtectionTokenProviderOptions>(
            options =>
            {
                options.TokenLifespan =
                    PasswordResetTokenLifetime;
            });

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name =
                SharedCookieAuthenticationDefaults.CookieName;

            options.Cookie.HttpOnly = true;

            options.Cookie.SecurePolicy =
                CookieSecurePolicy.Always;

            options.Cookie.SameSite =
                SameSiteMode.Lax;

            options.Cookie.Path = "/";

            options.Cookie.IsEssential = true;

            options.ExpireTimeSpan =
                AuthenticationCookieLifetime;

            options.SlidingExpiration = true;

            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode =
                    StatusCodes.Status401Unauthorized;

                return Task.CompletedTask;
            };

            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode =
                    StatusCodes.Status403Forbidden;

                return Task.CompletedTask;
            };
        });

        return services;
    }

    private static void ConfigureClaims(
        IdentityOptions options)
    {
        options.ClaimsIdentity.UserIdClaimType =
            ClaimTypes.NameIdentifier;

        options.ClaimsIdentity.UserNameClaimType =
            ClaimTypes.Name;

        options.ClaimsIdentity.RoleClaimType =
            ClaimTypes.Role;
    }

    private static void ConfigurePassword(
        IdentityOptions options)
    {
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireUppercase = true;
        options.Password.RequiredLength =
            PasswordRequiredLength;
        options.Password.RequiredUniqueChars =
            PasswordRequiredUniqueCharacters;
    }

    private static void ConfigureLockout(
        IdentityOptions options)
    {
        options.Lockout.AllowedForNewUsers = true;

        options.Lockout.DefaultLockoutTimeSpan =
            LockoutDuration;

        options.Lockout.MaxFailedAccessAttempts =
            MaximumFailedAccessAttempts;
    }

    private static void ConfigureSignIn(
        IdentityOptions options)
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.SignIn.RequireConfirmedEmail = true;
        options.SignIn.RequireConfirmedPhoneNumber = false;
    }

    private static void ConfigureUser(
        IdentityOptions options)
    {
        options.User.AllowedUserNameCharacters =
            "abcdefghijklmnopqrstuvwxyz" +
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ" +
            "0123456789-._@+";

        options.User.RequireUniqueEmail = true;
    }

    private static void ConfigureTokens(
        IdentityOptions options)
    {
        options.Tokens.EmailConfirmationTokenProvider =
            AppTokenProviders.EmailConfirmation;

        options.Tokens.PasswordResetTokenProvider =
            TokenOptions.DefaultProvider;
    }
}
```

### `src/SecureShop.Api/Program.cs`

Dosya uzantısı: `.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SecureShop.Api.Configuration;
using SecureShop.Api.Data;
using SecureShop.Api.Data.Seed;
using SecureShop.Api.Domain.Constants;
using SecureShop.Api.Features.Auth.External;
using SecureShop.Api.Features.Auth.TwoFactor;
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

builder.Services.AddControllers();

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

app.UseCookiePolicy();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await app.RunAsync();
```

### `src/SecureShop.Mvc/Security/SharedCookieAuthenticationDefaults.cs`

Dosya uzantısı: `.cs`

```csharp
namespace SecureShop.Mvc.Security;

public static class SharedCookieAuthenticationDefaults
{
    public const string AuthenticationScheme =
        "Identity.Application";

    public const string CookieName =
        "__Host-SecureShop.Auth";

    public const string DataProtectionApplicationName =
        "SecureShop.SharedCookie.v1";

    public const string KeyRingPathConfigurationKey =
        "Authentication:SharedCookie:KeyRingPath";
}
```

### `src/SecureShop.Mvc/Security/SharedCookieDataProtectionExtensions.cs`

Dosya uzantısı: `.cs`

```csharp
using Microsoft.AspNetCore.DataProtection;

namespace SecureShop.Mvc.Security;

public static class SharedCookieDataProtectionExtensions
{
    public static IServiceCollection AddSecureShopSharedCookieDataProtection(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var keyRingPath = ResolveKeyRingPath(
            configuration,
            environment);

        Directory.CreateDirectory(keyRingPath);

        var dataProtectionBuilder = services
            .AddDataProtection()
            .PersistKeysToFileSystem(
                new DirectoryInfo(keyRingPath))
            .SetApplicationName(
                SharedCookieAuthenticationDefaults
                    .DataProtectionApplicationName);

        if (OperatingSystem.IsWindows())
        {
            dataProtectionBuilder.ProtectKeysWithDpapi();
        }
        else if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Production ortamında ortak Data Protection anahtarları için " +
                "şifreli ve kalıcı bir key store yapılandırılmalıdır.");
        }

        return services;
    }

    private static string ResolveKeyRingPath(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var configuredPath = configuration[
            SharedCookieAuthenticationDefaults
                .KeyRingPathConfigurationKey];

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var expandedPath =
                Environment.ExpandEnvironmentVariables(
                    configuredPath.Trim());

            return Path.IsPathRooted(expandedPath)
                ? Path.GetFullPath(expandedPath)
                : Path.GetFullPath(
                    expandedPath,
                    environment.ContentRootPath);
        }

        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                $"'{SharedCookieAuthenticationDefaults.KeyRingPathConfigurationKey}' " +
                "production ortamında yapılandırılmalıdır.");
        }

        var localApplicationData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            throw new InvalidOperationException(
                "Yerel Data Protection anahtar dizini belirlenemedi.");
        }

        return Path.Combine(
            localApplicationData,
            "SecureShop",
            "DataProtection-Keys");
    }
}
```

### `src/SecureShop.Mvc/Http/AuthenticationDelegatingHandler.cs`

Dosya uzantısı: `.cs`

```csharp
using SecureShop.Mvc.Security;

namespace SecureShop.Mvc.Http;

public sealed class AuthenticationDelegatingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthenticationDelegatingHandler(
        IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        request.Headers.Remove("Cookie");

        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext is not null
            && httpContext.Request.Cookies.TryGetValue(
                SharedCookieAuthenticationDefaults.CookieName,
                out var authenticationCookie)
            && !string.IsNullOrWhiteSpace(authenticationCookie))
        {
            request.Headers.TryAddWithoutValidation(
                "Cookie",
                $"{SharedCookieAuthenticationDefaults.CookieName}={authenticationCookie}");
        }

        return base.SendAsync(request, cancellationToken);
    }
}
```

### `src/SecureShop.Mvc/Http/ApiResponse.cs`

Dosya uzantısı: `.cs`

```csharp
using System.Net;

namespace SecureShop.Mvc.Http;

public sealed record ApiResponse<T>(
    bool IsSuccess,
    HttpStatusCode StatusCode,
    T? Data,
    string? ErrorMessage)
{
    public static ApiResponse<T> Success(
        HttpStatusCode statusCode,
        T data)
    {
        ArgumentNullException.ThrowIfNull(data);

        return new ApiResponse<T>(
            IsSuccess: true,
            StatusCode: statusCode,
            Data: data,
            ErrorMessage: null);
    }

    public static ApiResponse<T> Failure(
        HttpStatusCode statusCode,
        string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        return new ApiResponse<T>(
            IsSuccess: false,
            StatusCode: statusCode,
            Data: default,
            ErrorMessage: errorMessage);
    }
}
```

### `src/SecureShop.Mvc/Services/Api/ApiSettings.cs`

Dosya uzantısı: `.cs`

```csharp
namespace SecureShop.Mvc.Services.Api;

public sealed class ApiSettings
{
    public const string SectionName = "ApiSettings";

    public string BaseUrl { get; set; } = string.Empty;
}
```

### `src/SecureShop.Mvc/Models/Responses/AuthSessionResponse.cs`

Dosya uzantısı: `.cs`

```csharp
namespace SecureShop.Mvc.Models.Responses;

public sealed record AuthSessionResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    string[] Roles,
    bool TwoFactorEnabled);
```

### `src/SecureShop.Mvc/Services/Interfaces/IAuthApiService.cs`

Dosya uzantısı: `.cs`

```csharp
using SecureShop.Mvc.Http;
using SecureShop.Mvc.Models.Responses;

namespace SecureShop.Mvc.Services.Interfaces;

public interface IAuthApiService
{
    Task<ApiResponse<AuthSessionResponse>> GetSessionAsync(
        CancellationToken cancellationToken = default);
}
```

### `src/SecureShop.Mvc/Services/Api/AuthApiService.cs`

Dosya uzantısı: `.cs`

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

### `src/SecureShop.Mvc/Models/ViewModels/AuthSessionViewModel.cs`

Dosya uzantısı: `.cs`

```csharp
using SecureShop.Mvc.Models.Responses;

namespace SecureShop.Mvc.Models.ViewModels;

public sealed class AuthSessionViewModel
{
    public bool MvcIsAuthenticated { get; init; }

    public string? MvcUserName { get; init; }

    public int ApiStatusCode { get; init; }

    public AuthSessionResponse? ApiSession { get; init; }

    public string? ErrorMessage { get; init; }
}
```

### `src/SecureShop.Mvc/Controllers/AccountController.cs`

Dosya uzantısı: `.cs`

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

### `src/SecureShop.Mvc/Views/Account/Session.cshtml`

Dosya uzantısı: `.cshtml`

```cshtml
@model SecureShop.Mvc.Models.ViewModels.AuthSessionViewModel

@{
    ViewData["Title"] = "Oturum Denetimi";
}

<div class="container py-4">
    <h1 class="mb-4">Oturum Denetimi</h1>

    <section class="card mb-4">
        <div class="card-body">
            <h2 class="h5">MVC Authentication Durumu</h2>

            @if (Model.MvcIsAuthenticated)
            {
                <p class="mb-2">
                    MVC ortak authentication cookie'sini doğruladı.
                </p>

                <dl class="row mb-0">
                    <dt class="col-sm-4">Authenticated</dt>
                    <dd class="col-sm-8">Evet</dd>

                    <dt class="col-sm-4">Kullanıcı adı</dt>
                    <dd class="col-sm-8">
                        @(Model.MvcUserName ?? "-")
                    </dd>
                </dl>
            }
            else
            {
                <div class="alert alert-warning mb-0">
                    MVC tarafında doğrulanmış bir kullanıcı bulunmuyor.
                </div>
            }
        </div>
    </section>

    <section class="card mb-4">
        <div class="card-body">
            <h2 class="h5">API Authentication Durumu</h2>

            <p>
                HTTP durum kodu:
                <strong>@Model.ApiStatusCode</strong>
            </p>

            @if (Model.ApiSession is not null)
            {
                <div class="alert alert-success">
                    Authentication cookie MVC üzerinden API'ye başarıyla aktarıldı.
                </div>

                <dl class="row">
                    <dt class="col-sm-4">Kullanıcı kimliği</dt>
                    <dd class="col-sm-8">@Model.ApiSession.UserId</dd>

                    <dt class="col-sm-4">E-posta</dt>
                    <dd class="col-sm-8">@Model.ApiSession.Email</dd>

                    <dt class="col-sm-4">Ad soyad</dt>
                    <dd class="col-sm-8">@Model.ApiSession.DisplayName</dd>

                    <dt class="col-sm-4">Roller</dt>
                    <dd class="col-sm-8">
                        @if (Model.ApiSession.Roles.Length == 0)
                        {
                            <span>-</span>
                        }
                        else
                        {
                            @string.Join(", ", Model.ApiSession.Roles)
                        }
                    </dd>

                    <dt class="col-sm-4">TOTP etkin</dt>
                    <dd class="col-sm-8">
                        @(Model.ApiSession.TwoFactorEnabled ? "Evet" : "Hayır")
                    </dd>
                </dl>
            }
            else
            {
                <div class="alert alert-secondary mb-0">
                    @(Model.ErrorMessage ?? "API oturum bilgisi bulunamadı.")
                </div>
            }
        </div>
    </section>

    @if (Model.MvcIsAuthenticated)
    {
        <form asp-controller="Account"
              asp-action="Logout"
              method="post">
            <button type="submit" class="btn btn-danger">
                Oturumu Kapat
            </button>
        </form>
    }
</div>
```

### `src/SecureShop.Mvc/Program.cs`

Dosya uzantısı: `.cs`

```csharp
using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using SecureShop.Mvc.Http;
using SecureShop.Mvc.Security;
using SecureShop.Mvc.Services.Api;
using SecureShop.Mvc.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddSecureShopSharedCookieDataProtection(
    builder.Configuration,
    builder.Environment);

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            SharedCookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme =
            SharedCookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultSignInScheme =
            SharedCookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultSignOutScheme =
            SharedCookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultForbidScheme =
            SharedCookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(
        SharedCookieAuthenticationDefaults.AuthenticationScheme,
        options =>
        {
            options.Cookie.Name =
                SharedCookieAuthenticationDefaults.CookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.Path = "/";
            options.Cookie.IsEssential = true;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(20);
            options.SlidingExpiration = true;
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/Forbidden";
        });

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

builder.Services
    .AddOptions<ApiSettings>()
    .Bind(builder.Configuration.GetSection(ApiSettings.SectionName))
    .Validate(
        options =>
            Uri.TryCreate(
                options.BaseUrl,
                UriKind.Absolute,
                out var uri)
            && uri.Scheme == Uri.UriSchemeHttps,
        "ApiSettings:BaseUrl geçerli bir HTTPS adresi olmalıdır.")
    .ValidateOnStart();

builder.Services.AddTransient<AuthenticationDelegatingHandler>();

builder.Services
    .AddHttpClient<IAuthApiService, AuthApiService>(
        (serviceProvider, client) =>
        {
            var apiSettings = serviceProvider
                .GetRequiredService<IOptions<ApiSettings>>()
                .Value;

            client.BaseAddress = new Uri(
                $"{apiSettings.BaseUrl.TrimEnd('/')}/",
                UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue(
                    "application/json"));
        })
    .ConfigurePrimaryHttpMessageHandler(
        () => new SocketsHttpHandler
        {
            UseCookies = false,
            AllowAutoRedirect = false,
            AutomaticDecompression =
                DecompressionMethods.GZip
                | DecompressionMethods.Deflate
                | DecompressionMethods.Brotli
        })
    .AddHttpMessageHandler<AuthenticationDelegatingHandler>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseCookiePolicy();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

await app.RunAsync();
```

### `src/SecureShop.Mvc/appsettings.json`

Dosya uzantısı: `.json`

```json
{
  "ApiSettings": {
    "BaseUrl": "https://localhost:7001/"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```
