# Modern Navbar Login/Logout

## Amaç

Navbar authentication durumuna göre modern bir giriş butonu veya kullanıcı dropdown menüsü gösterir. Logout yalnızca POST ve antiforgery token ile yapılır.

## Davranış

- Anonim: yuvarlatılmış “Giriş Yap” butonu.
- Authenticated: baş harf avatarı, kullanıcı adı, oturum bilgileri ve güvenli çıkış.
- Mobil: tam genişlikte auth kontrolü ve responsive collapse.
- Razor çıktıları otomatik HTML encode edilir.

## Doğrulama

```text
MVC Release build       : 0 uyarı, 0 hata
Modern navbar render    : başarılı
Anonim login butonu     : mevcut
Login antiforgery       : mevcut
Anonim logout görünümü  : gizli
```

Not: Bu ortamda kullanılabilir görsel tarayıcı bulunmadığından ekran görüntüsü alınamadı; render edilen HTML doğrulandı.

# Dosya bazında eksiksiz kodlar

## `src/SecureShop.Mvc/Views/Shared/_AuthenticationMenu.cshtml`

Uzantı: `.cshtml`

```cshtml
@{
    var isAuthenticated = User.Identity?.IsAuthenticated == true;
    var userName = User.Identity?.Name ?? "Kullanıcı";
    var initial = userName.Length > 0
        ? userName[..1].ToUpperInvariant()
        : "K";
}

<ul class="navbar-nav align-items-sm-center auth-menu">
    @if (isAuthenticated)
    {
        <li class="nav-item dropdown">
            <button class="btn auth-user-button dropdown-toggle"
                    type="button"
                    data-bs-toggle="dropdown"
                    aria-expanded="false"
                    aria-label="Kullanıcı menüsünü aç">
                <span class="auth-avatar" aria-hidden="true">@initial</span>
                <span class="auth-user-name d-none d-md-inline">@userName</span>
            </button>
            <ul class="dropdown-menu dropdown-menu-end auth-dropdown shadow-lg">
                <li class="auth-dropdown-header">
                    <span class="small text-body-secondary">Oturum açıldı</span>
                    <strong class="d-block text-truncate">@userName</strong>
                </li>
                <li><hr class="dropdown-divider" /></li>
                <li>
                    <a class="dropdown-item" asp-controller="Account" asp-action="Session">
                        Oturum bilgileri
                    </a>
                </li>
                <li><hr class="dropdown-divider" /></li>
                <li>
                    <form asp-controller="Account"
                          asp-action="Logout"
                          asp-antiforgery="false"
                          method="post">
                        @Html.AntiForgeryToken()
                        <button class="dropdown-item text-danger" type="submit">
                            Güvenli çıkış
                        </button>
                    </form>
                </li>
            </ul>
        </li>
    }
    else
    {
        <li class="nav-item mt-2 mt-sm-0">
            <a class="btn auth-login-button"
               asp-controller="Account"
               asp-action="Login">
                Giriş Yap
            </a>
        </li>
    }
</ul>
```

## `src/SecureShop.Mvc/Views/Shared/_Layout.cshtml`

Uzantı: `.cshtml`

```cshtml
<!DOCTYPE html>
<html lang="tr">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@ViewData["Title"] - SecureShop</title>
    <script type="importmap"></script>
    <link rel="stylesheet" href="~/lib/bootstrap/dist/css/bootstrap.min.css" />
    <link rel="stylesheet" href="~/css/site.css" asp-append-version="true" />
    <link rel="stylesheet" href="~/SecureShop.Mvc.styles.css" asp-append-version="true" />
</head>
<body>
    <header>
        <nav class="navbar navbar-expand-sm navbar-dark secure-navbar shadow-sm mb-4">
            <div class="container">
                <a class="navbar-brand secure-brand" asp-controller="Home" asp-action="Index">
                    <span class="brand-mark" aria-hidden="true">S</span>
                    <span>SecureShop</span>
                </a>
                <button class="navbar-toggler" type="button" data-bs-toggle="collapse" data-bs-target=".navbar-collapse" aria-controls="navbarSupportedContent"
                        aria-expanded="false" aria-label="Toggle navigation">
                    <span class="navbar-toggler-icon"></span>
                </button>
                <div id="navbarSupportedContent" class="navbar-collapse collapse">
                    <ul class="navbar-nav me-auto">
                        <li class="nav-item">
                            <a class="nav-link" asp-controller="Home" asp-action="Index">Ana Sayfa</a>
                        </li>
                        <li class="nav-item">
                            <a class="nav-link" asp-controller="Products" asp-action="Index">Ürünler</a>
                        </li>
                    </ul>
                    <partial name="_AuthenticationMenu" />
                </div>
            </div>
        </nav>
    </header>
    <div class="container">
        <main role="main" class="pb-3">
            @RenderBody()
        </main>
    </div>

    <footer class="border-top footer text-muted">
        <div class="container">
            &copy; 2026 SecureShop
        </div>
    </footer>
    <script src="~/lib/jquery/dist/jquery.min.js"></script>
    <script src="~/lib/bootstrap/dist/js/bootstrap.bundle.min.js"></script>
    <script src="~/js/site.js" asp-append-version="true"></script>
    @await RenderSectionAsync("Scripts", required: false)
</body>
</html>
```

## `src/SecureShop.Mvc/wwwroot/css/site.css`

Uzantı: `.css`

```css
html {
  font-size: 14px;
}

@media (min-width: 768px) {
  html {
    font-size: 16px;
  }
}

.btn:focus, .btn:active:focus, .btn-link.nav-link:focus, .form-control:focus, .form-check-input:focus {
  box-shadow: 0 0 0 0.1rem white, 0 0 0 0.25rem #258cfb;
}

html {
  position: relative;
  min-height: 100%;
}

body {
  margin-bottom: 60px;
  background: #f5f7fb;
  color: #172033;
}

.secure-navbar {
  background: linear-gradient(120deg, #111827, #1e3a8a 70%, #2563eb);
  min-height: 4.5rem;
}

.secure-brand { display: inline-flex; align-items: center; gap: .65rem; font-weight: 700; }
.brand-mark, .auth-avatar { display: inline-grid; place-items: center; border-radius: 50%; font-weight: 700; }
.brand-mark { width: 2.1rem; height: 2.1rem; background: #fff; color: #1d4ed8; }
.secure-navbar .nav-link { color: rgba(255,255,255,.78); font-weight: 500; }
.secure-navbar .nav-link:hover, .secure-navbar .nav-link:focus { color: #fff; }
.auth-menu { margin-left: auto; }
.auth-login-button { color: #111827; background: #fff; border: 1px solid rgba(255,255,255,.75); border-radius: 999px; padding: .55rem 1.15rem; font-weight: 650; }
.auth-login-button:hover { color: #fff; background: transparent; }
.auth-user-button { display: inline-flex; align-items: center; gap: .55rem; color: #fff; border: 1px solid rgba(255,255,255,.25); border-radius: 999px; padding: .35rem .8rem .35rem .4rem; }
.auth-user-button:hover, .auth-user-button:focus { color: #fff; background: rgba(255,255,255,.12); }
.auth-avatar { width: 2rem; height: 2rem; background: #dbeafe; color: #1d4ed8; }
.auth-user-name { max-width: 13rem; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.auth-dropdown { min-width: 16rem; border: 0; border-radius: .85rem; padding: .55rem; }
.auth-dropdown-header { padding: .5rem .75rem; max-width: 17rem; }
.auth-dropdown .dropdown-item { border-radius: .55rem; padding: .6rem .75rem; }

@media (max-width: 575.98px) {
  .auth-menu { align-items: stretch !important; margin-top: .75rem; }
  .auth-user-button, .auth-login-button { width: 100%; justify-content: center; }
  .auth-dropdown { width: 100%; }
}

.form-floating > .form-control-plaintext::placeholder, .form-floating > .form-control::placeholder {
  color: var(--bs-secondary-color);
  text-align: end;
}

.form-floating > .form-control-plaintext:focus::placeholder, .form-floating > .form-control:focus::placeholder {
  text-align: start;
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
    private readonly IAuthApiService _authApiService;

    public AccountController(IAuthApiService authApiService)
    {
        _authApiService = authApiService;
    }

    [AllowAnonymous]
    [HttpGet("login")]
    public IActionResult Login()
    {
        return User.Identity?.IsAuthenticated == true
            ? RedirectToAction(nameof(Session))
            : View(new LoginViewModel());
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

        return RedirectToAction(nameof(Session));
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

