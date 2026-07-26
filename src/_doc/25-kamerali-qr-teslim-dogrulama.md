# 1. Bu Adımın Amacı

Çalışan teslim doğrulama adresi token olmadan açıldığında hata göstermek yerine
telefonun arka kamerasını kullanarak müşterinin sipariş QR kodunu taramak; geçerli
kod bulunduğunda aynı origin üzerindeki güvenli teslim onay ekranına otomatik geçmek.

# 2. Etkilenen Uygulama

```text
SecureShop.Mvc
SecureShop.Mvc.Tests
```

API, Entity Framework modeli ve SQL Server şeması değişmedi; migration gerekmez.

# 3. Bu Adımda Yapılacaklar

1. Token yokken kamera tarayıcı arayüzünü göstermek.
2. Arka kamera izni istemek ve canlı görüntüyü ekrana vermek.
3. Yerleşik BarcodeDetector ile yalnızca QR kodlarını okumak.
4. Okunan URL için origin, path ve token doğrulaması yapmak.
5. Geçerli QR bulunduğunda tokenlı doğrulama ekranına yönlendirmek.
6. Kamera desteklenmiyorsa ana kamera uygulaması ve görsel seçme alternatifini sunmak.
7. Sayfa kapanırken kamera akışını ve zamanlayıcıyı durdurmak.
8. Permissions-Policy ile kamera erişimini yalnızca aynı origin için açmak.
9. Token bulunan ve bulunmayan controller davranışlarını regresyon testine almak.

# 4. CLI Komutları

Çalışma dizini: `D:\Code\ASP.NET\SecureShop`

```powershell
dotnet build SecureShop.sln -c Release --no-restore
dotnet test SecureShop.sln -c Release --no-build
node --check src\SecureShop.Mvc\wwwroot\js\qr-scanner.js
```

Uygulamalar:

```powershell
dotnet watch --project src\SecureShop.Api --urls https://localhost:7001
dotnet watch --project src\SecureShop.Mvc --urls https://localhost:7002
```

# 5. Güncel Proje Yapısı

```text
src/SecureShop.Mvc/
├── Controllers/EmployeeOrdersController.cs
├── Program.cs
├── Views/EmployeeOrders/Verify.cshtml
└── wwwroot/
    ├── css/site.css
    └── js/qr-scanner.js
tests/SecureShop.Mvc.Tests/
└── EmployeeOrdersControllerTests.cs
```

# 6. Dosya Bazında Eksiksiz Kodlar

## `src\SecureShop.Mvc\Controllers\EmployeeOrdersController.cs`

Uzantı: `.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureShop.Mvc.Http;
using SecureShop.Mvc.Models.Requests;
using SecureShop.Mvc.Models.Responses;
using SecureShop.Mvc.Models.ViewModels;
using SecureShop.Mvc.Security;
using SecureShop.Mvc.Services.Interfaces;

namespace SecureShop.Mvc.Controllers;

[Authorize(Roles = AppRoles.Admin + "," + AppRoles.Employee)]
[Route("employee/orders")]
public sealed class EmployeeOrdersController : Controller
{
    private readonly IOrderApiService _orderApiService;

    public EmployeeOrdersController(
        IOrderApiService orderApiService)
    {
        _orderApiService = orderApiService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken)
    {
        var result = await _orderApiService.GetStaffAsync(
            cancellationToken);

        return View(new OrderListViewModel
        {
            Orders = result.Data ?? [],
            ErrorMessage = result.ErrorMessage
        });
    }

    [HttpGet("{orderNumber}")]
    public async Task<IActionResult> Details(
        string orderNumber,
        CancellationToken cancellationToken)
    {
        var result = await _orderApiService.GetStaffAsync(
            orderNumber,
            cancellationToken);

        if (!result.IsSuccess || result.Data is null)
        {
            TempData["ErrorMessage"] =
                result.ErrorMessage ?? "Sipariş bulunamadı.";

            return RedirectToAction(nameof(Index));
        }

        return View(result.Data);
    }

    [HttpPost("{orderNumber}/approve")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Approve(
        string orderNumber,
        string rowVersion,
        CancellationToken cancellationToken) =>
        ProcessAsync(
            orderNumber,
            () => _orderApiService.ApproveAsync(
                orderNumber,
                new ProcessOrderRequest(rowVersion),
                cancellationToken));

    [HttpPost("{orderNumber}/ready")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> MarkReady(
        string orderNumber,
        string rowVersion,
        CancellationToken cancellationToken) =>
        ProcessAsync(
            orderNumber,
            () => _orderApiService.MarkReadyAsync(
                orderNumber,
                new ProcessOrderRequest(rowVersion),
                cancellationToken));

    [HttpPost("{orderNumber}/cancel")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Cancel(
        string orderNumber,
        string rowVersion,
        CancellationToken cancellationToken) =>
        ProcessAsync(
            orderNumber,
            () => _orderApiService.CancelAsync(
                orderNumber,
                new ProcessOrderRequest(rowVersion),
                cancellationToken));

    [HttpGet("verify")]
    public IActionResult Verify([FromQuery] string? token) =>
        View(new QrVerificationViewModel
        {
            Token = token ?? string.Empty
        });

    [HttpPost("verify")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Verify(
        QrVerificationViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _orderApiService.VerifyQrAsync(
            new VerifyOrderQrRequest(model.Token),
            cancellationToken);

        if (!result.IsSuccess || result.Data is null)
        {
            ModelState.AddModelError(
                string.Empty,
                result.ErrorMessage ?? "QR doğrulanamadı.");

            return View(model);
        }

        model.Order = result.Data;
        TempData["SuccessMessage"] =
            "QR doğrulandı; sipariş teslim edildi olarak tamamlandı.";

        return View(model);
    }

    private async Task<IActionResult> ProcessAsync(
        string orderNumber,
        Func<Task<ApiResponse<OrderResponse>>> operation)
    {
        var result = await operation();

        TempData[result.IsSuccess
            ? "SuccessMessage"
            : "ErrorMessage"] = result.IsSuccess
                ? "Sipariş durumu güncellendi."
                : result.ErrorMessage
                    ?? "Sipariş durumu güncellenemedi.";

        return RedirectToAction(
            nameof(Details),
            new
            {
                orderNumber
            });
    }
}
```

## `src\SecureShop.Mvc\Program.cs`

Uzantı: `.cs`

```csharp
using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using SecureShop.Mvc.Http;
using SecureShop.Mvc.Security;
using SecureShop.Mvc.Services.Api;
using SecureShop.Mvc.Services.Interfaces;
using SecureShop.Mvc.Services.Storage;

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
builder.Services.AddScoped<IProductImageStorage, ProductImageStorage>();

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

builder.Services
    .AddHttpClient<IProductApiService, ProductApiService>(
        (serviceProvider, client) =>
        {
            var apiSettings = serviceProvider.GetRequiredService<IOptions<ApiSettings>>().Value;
            client.BaseAddress = new Uri($"{apiSettings.BaseUrl.TrimEnd('/')}/", UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        })
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        UseCookies = false,
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.GZip
            | DecompressionMethods.Deflate
            | DecompressionMethods.Brotli
    })
    .AddHttpMessageHandler<AuthenticationDelegatingHandler>();

builder.Services
    .AddHttpClient<ICartApiService, CartApiService>(
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

builder.Services
    .AddHttpClient<IOrderApiService, OrderApiService>(
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

builder.Services
    .AddHttpClient<IAuditApiService, AuditApiService>(
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
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] =
        "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] =
        "camera=(self), microphone=(), geolocation=()";
    context.Response.Headers.ContentSecurityPolicy =
        "default-src 'self'; img-src 'self' data:; " +
        "style-src 'self' 'unsafe-inline'; script-src 'self'; " +
        "font-src 'self'; frame-ancestors 'none'; base-uri 'self'; " +
        "form-action 'self';";

    await next();
});
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

## `src\SecureShop.Mvc\Views\EmployeeOrders\Verify.cshtml`

Uzantı: `.cshtml`

```cshtml
@model SecureShop.Mvc.Models.ViewModels.QrVerificationViewModel
@{
    ViewData["Title"] = "Sipariş QR doğrulama";
    var hasToken = !string.IsNullOrWhiteSpace(Model.Token);
}

<div class="row justify-content-center">
    <div class="col-lg-7">
        <div class="card border-0 shadow-sm qr-verification-card">
            <div class="card-body p-4 p-lg-5">
                <span class="text-uppercase text-primary fw-semibold small">
                    Güvenli teslim
                </span>
                <h1 class="mt-2">QR teslim doğrulaması</h1>

                @if (TempData["SuccessMessage"] is string successMessage)
                {
                    <div class="alert alert-success">@successMessage</div>
                }

                @if (Model.Order is not null)
                {
                    <div class="qr-verification-success text-center py-4">
                        <div class="display-5 mb-3" aria-hidden="true">✓</div>
                        <h2 class="h4">@Model.Order.OrderNumber</h2>
                        <p class="text-success mb-0">
                            Sipariş teslim edildi olarak tamamlandı.
                        </p>
                    </div>
                }
                else if (hasToken)
                {
                    <p class="text-body-secondary">
                        QR bağlantısı doğrulama için hazır. Teslim alan müşteriyi
                        kontrol ettikten sonra işlemi onaylayın.
                    </p>
                    <form asp-action="Verify" method="post">
                        <div asp-validation-summary="ModelOnly"
                             class="alert alert-danger"></div>
                        <input asp-for="Token" type="hidden" />
                        <button class="btn btn-primary btn-lg w-100 mt-3"
                                type="submit">
                            Teslimi doğrula ve siparişi tamamla
                        </button>
                    </form>
                }
                else
                {
                    <div class="qr-scanner" data-qr-scanner>
                        <p class="text-body-secondary">
                            Telefon kamerasını açıp müşterinin sipariş detayında
                            gösterilen QR koduna yöneltin.
                        </p>

                        <div class="qr-scanner-status alert alert-info"
                             data-qr-scanner-status
                             role="status"
                             aria-live="polite">
                            Kamera henüz başlatılmadı.
                        </div>

                        <div class="qr-scanner-stage d-none"
                             data-qr-scanner-stage>
                            <video data-qr-scanner-video
                                   playsinline
                                   muted
                                   aria-label="QR tarama kamerası"></video>
                            <div class="qr-scanner-frame"
                                 aria-hidden="true"></div>
                        </div>

                        <div class="d-flex flex-wrap gap-2 mt-3">
                            <button type="button"
                                    class="btn btn-primary"
                                    data-qr-scanner-start>
                                Kamerayı aç
                            </button>
                            <button type="button"
                                    class="btn btn-outline-secondary d-none"
                                    data-qr-scanner-stop>
                                Kamerayı kapat
                            </button>
                        </div>

                        <div class="qr-scanner-upload mt-4">
                            <label for="qr-image-upload"
                                   class="form-label fw-semibold">
                                Alternatif: QR fotoğrafı seç
                            </label>
                            <input id="qr-image-upload"
                                   type="file"
                                   accept="image/*"
                                   capture="environment"
                                   class="form-control"
                                   data-qr-scanner-upload />
                            <div class="form-text">
                                Kamera açılmazsa QR kodunun fotoğrafını çekip
                                buradan seçebilirsiniz.
                            </div>
                        </div>
                    </div>
                }
            </div>
        </div>
    </div>
</div>

@section Scripts {
    <partial name="_ValidationScriptsPartial" />
    @if (!hasToken && Model.Order is null)
    {
        <script src="~/js/qr-scanner.js"
                asp-append-version="true"></script>
    }
}
```

## `src\SecureShop.Mvc\wwwroot\js\qr-scanner.js`

Uzantı: `.js`

```javascript
(() => {
    "use strict";

    const scanner = document.querySelector("[data-qr-scanner]");

    if (!scanner) {
        return;
    }

    const startButton = scanner.querySelector("[data-qr-scanner-start]");
    const stopButton = scanner.querySelector("[data-qr-scanner-stop]");
    const stage = scanner.querySelector("[data-qr-scanner-stage]");
    const video = scanner.querySelector("[data-qr-scanner-video]");
    const status = scanner.querySelector("[data-qr-scanner-status]");
    const upload = scanner.querySelector("[data-qr-scanner-upload]");
    let detector = null;
    let stream = null;
    let scanTimer = null;
    let isDetecting = false;

    const setStatus = (message, type = "info") => {
        status.textContent = message;
        status.className = `qr-scanner-status alert alert-${type}`;
    };

    const createDetector = async () => {
        if (!("BarcodeDetector" in window)) {
            throw new Error(
                "Bu tarayıcı QR algılamayı desteklemiyor. Telefonun ana kamera uygulamasıyla QR kodunu tarayın.");
        }

        const supportedFormats =
            await window.BarcodeDetector.getSupportedFormats();

        if (!supportedFormats.includes("qr_code")) {
            throw new Error(
                "Bu tarayıcı QR kod biçimini desteklemiyor.");
        }

        return new window.BarcodeDetector({
            formats: ["qr_code"]
        });
    };

    const stopCamera = () => {
        if (scanTimer !== null) {
            window.clearTimeout(scanTimer);
            scanTimer = null;
        }

        stream?.getTracks().forEach((track) => track.stop());
        stream = null;
        video.srcObject = null;
        stage.classList.add("d-none");
        startButton.classList.remove("d-none");
        stopButton.classList.add("d-none");
        isDetecting = false;
    };

    const openVerificationUrl = (rawValue) => {
        let scannedUrl;

        try {
            scannedUrl = new URL(rawValue);
        } catch {
            setStatus(
                "Okunan QR geçerli bir web adresi içermiyor.",
                "danger");
            return false;
        }

        const expectedPath = "/employee/orders/verify";
        const token = scannedUrl.searchParams.get("token");

        if (scannedUrl.origin !== window.location.origin
            || scannedUrl.pathname.toLowerCase()
                !== expectedPath.toLowerCase()
            || !token) {
            setStatus(
                "Bu QR SecureShop teslim doğrulama kodu değil.",
                "danger");
            return false;
        }

        stopCamera();
        setStatus(
            "QR bulundu. Güvenli doğrulama ekranı açılıyor.",
            "success");
        window.location.assign(
            `${expectedPath}?token=${encodeURIComponent(token)}`);
        return true;
    };

    const detectFromSource = async (source) => {
        if (!detector) {
            detector = await createDetector();
        }

        const barcodes = await detector.detect(source);

        for (const barcode of barcodes) {
            if (barcode.rawValue
                && openVerificationUrl(barcode.rawValue)) {
                return true;
            }
        }

        return false;
    };

    const scanFrame = async () => {
        if (!stream || isDetecting) {
            return;
        }

        isDetecting = true;

        try {
            await detectFromSource(video);
        } catch (error) {
            stopCamera();
            setStatus(
                error instanceof Error
                    ? error.message
                    : "QR taraması tamamlanamadı.",
                "danger");
            return;
        } finally {
            isDetecting = false;
        }

        if (stream) {
            scanTimer = window.setTimeout(scanFrame, 250);
        }
    };

    const startCamera = async () => {
        if (!window.isSecureContext) {
            setStatus(
                "Kamera yalnızca HTTPS bağlantısında kullanılabilir.",
                "danger");
            return;
        }

        if (!navigator.mediaDevices?.getUserMedia) {
            setStatus(
                "Bu tarayıcı kamera erişimini desteklemiyor.",
                "danger");
            return;
        }

        startButton.disabled = true;
        setStatus("Kamera izni bekleniyor…");

        try {
            detector = await createDetector();
            stream = await navigator.mediaDevices.getUserMedia({
                audio: false,
                video: {
                    facingMode: {
                        ideal: "environment"
                    },
                    width: {
                        ideal: 1280
                    },
                    height: {
                        ideal: 720
                    }
                }
            });
            video.srcObject = stream;
            await video.play();
            stage.classList.remove("d-none");
            startButton.classList.add("d-none");
            stopButton.classList.remove("d-none");
            setStatus(
                "Kamera açık. QR kodunu çerçevenin içine getirin.",
                "success");
            scanTimer = window.setTimeout(scanFrame, 150);
        } catch (error) {
            stopCamera();
            setStatus(
                error instanceof Error
                    ? error.message
                    : "Kamera başlatılamadı.",
                "danger");
        } finally {
            startButton.disabled = false;
        }
    };

    const scanUploadedImage = async (file) => {
        if (!file.type.startsWith("image/")) {
            setStatus("Lütfen bir görsel dosyası seçin.", "danger");
            return;
        }

        setStatus("QR fotoğrafı inceleniyor…");

        try {
            const image = await createImageBitmap(file);
            const found = await detectFromSource(image);
            image.close();

            if (!found) {
                setStatus(
                    "Fotoğrafta okunabilir bir QR kod bulunamadı.",
                    "warning");
            }
        } catch (error) {
            setStatus(
                error instanceof Error
                    ? error.message
                    : "QR fotoğrafı okunamadı.",
                "danger");
        }
    };

    startButton.addEventListener("click", () => {
        void startCamera();
    });
    stopButton.addEventListener("click", () => {
        stopCamera();
        setStatus("Kamera kapatıldı.");
    });
    upload.addEventListener("change", () => {
        const file = upload.files?.[0];
        if (file) {
            void scanUploadedImage(file);
        }
    });
    window.addEventListener("pagehide", stopCamera);
})();
```

## `src\SecureShop.Mvc\wwwroot\css\site.css`

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
.nav-cart-link { display: inline-flex; align-items: center; gap: .4rem; }
.nav-cart-icon { font-size: 1.05rem; }
.nav-cart-count {
  display: inline-grid;
  min-width: 1.55rem;
  height: 1.55rem;
  place-items: center;
  padding: 0 .35rem;
  border-radius: 999px;
  background: #fbbf24;
  color: #172033;
  font-size: .75rem;
  font-weight: 800;
  line-height: 1;
}
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

.cart-item-card, .cart-summary-card, .cart-empty-state { border-radius: 1rem; }
.cart-item-card { overflow: hidden; transition: transform .16s ease, box-shadow .16s ease; }
.cart-item-card:hover { transform: translateY(-1px); }
.cart-summary-card { position: sticky; top: 1.5rem; }
.cart-empty-icon { font-size: 3rem; }
.cart-heading-count {
  padding: .35rem .7rem;
  border-radius: 999px;
  background: #e0e7ff;
  color: #3730a3;
  font-size: .85rem;
}
.cart-list-header {
  display: flex;
  justify-content: space-between;
  padding: 0 .25rem;
  color: #64748b;
  font-size: .8rem;
  font-weight: 700;
  letter-spacing: .04em;
  text-transform: uppercase;
}
.cart-item-grid {
  display: grid;
  grid-template-columns: 8.5rem minmax(0, 1fr) auto;
  gap: 1.25rem;
  align-items: center;
}
.cart-item-image-link {
  display: grid;
  width: 8.5rem;
  height: 8.5rem;
  place-items: center;
  overflow: hidden;
  border: 1px solid #e2e8f0;
  border-radius: .9rem;
  background: #fff;
}
.cart-item-image {
  width: 100%;
  height: 100%;
  padding: .5rem;
  object-fit: contain;
  transition: transform .18s ease;
}
.cart-item-image-link:hover .cart-item-image { transform: scale(1.04); }
.cart-item-image-placeholder { font-size: 2.5rem; }
.cart-item-title {
  color: #172033;
  font-size: 1.05rem;
  font-weight: 750;
  line-height: 1.35;
  text-decoration: none;
}
.cart-item-title:hover { color: #1d4ed8; }
.cart-stock-status {
  margin-top: .55rem;
  font-size: .8rem;
  font-weight: 650;
}
.cart-stock-status.is-available { color: #15803d; }
.cart-stock-status.is-unavailable { color: #b91c1c; }
.cart-item-price {
  min-width: 7rem;
  align-self: start;
  text-align: right;
}
.cart-item-price strong {
  display: block;
  margin-top: .25rem;
  color: #0f172a;
  font-size: 1.1rem;
}
.cart-quantity-form {
  display: inline-flex;
  min-height: 2.4rem;
  align-items: center;
  overflow: hidden;
  border: 1px solid #cbd5e1;
  border-radius: 999px;
  background: #fff;
  transition: border-color .15s ease, box-shadow .15s ease, opacity .15s ease;
}
.cart-quantity-form:focus-within {
  border-color: #2563eb;
  box-shadow: 0 0 0 .2rem rgba(37, 99, 235, .13);
}
.cart-quantity-form.is-updating { opacity: .68; }
.cart-quantity-form input {
  width: 3rem;
  border: 0;
  outline: 0;
  background: transparent;
  font-weight: 750;
  text-align: center;
  appearance: textfield;
  -moz-appearance: textfield;
}
.cart-quantity-form input::-webkit-inner-spin-button,
.cart-quantity-form input::-webkit-outer-spin-button { margin: 0; appearance: none; }
.cart-quantity-button {
  width: 2.35rem;
  align-self: stretch;
  border: 0;
  background: #f8fafc;
  color: #1e3a8a;
  font-size: 1.15rem;
  font-weight: 800;
  transition: background .12s ease;
}
.cart-quantity-button:hover:not(:disabled) { background: #dbeafe; }
.cart-quantity-button:disabled { color: #94a3b8; cursor: not-allowed; }
.cart-quantity-spinner {
  display: none;
  width: .85rem;
  height: .85rem;
  margin-right: .7rem;
}
.cart-quantity-form.is-updating .cart-quantity-spinner { display: inline-block; }
.cart-grand-total {
  align-items: center;
  font-size: 1.1rem;
}
.cart-grand-total strong {
  color: #0f172a;
  font-size: 1.45rem;
}

.product-card { border-radius: 1rem; overflow: hidden; }
.product-card-image-link { display: block; background: #fff; }
.product-card-image { height: 15rem; object-fit: contain; padding: 1rem; transition: transform .2s ease; }
.product-card:hover .product-card-image { transform: scale(1.035); }

.product-detail-card { border-radius: 1.25rem; }
.product-gallery { display: grid; grid-template-columns: 5.25rem minmax(0, 1fr); gap: 1rem; }
.product-gallery-thumbnails { display: flex; flex-direction: column; gap: .75rem; }
.product-gallery-thumbnail { width: 5.25rem; height: 5.25rem; padding: .25rem; border: 2px solid #dbe2ea; border-radius: .75rem; background: #fff; transition: border-color .15s ease, box-shadow .15s ease; }
.product-gallery-thumbnail:hover { border-color: #94a3b8; }
.product-gallery-thumbnail.is-active { border-color: #2563eb; box-shadow: 0 0 0 .2rem rgba(37, 99, 235, .15); }
.product-gallery-thumbnail img { width: 100%; height: 100%; object-fit: contain; }
.product-gallery-stage { min-height: 34rem; display: grid; place-items: center; overflow: hidden; border-radius: 1rem; background: #fff; }
.product-gallery-main-image { width: 100%; height: 34rem; object-fit: contain; transition: transform .25s ease; }
.product-gallery-stage:hover .product-gallery-main-image { transform: scale(1.06); }
.product-gallery-empty { min-height: 28rem; display: grid; place-items: center; border: 1px dashed #cbd5e1; border-radius: 1rem; color: #64748b; }
.product-detail-description { color: #475569; }
.product-detail-price { margin: 1.5rem 0 .5rem; font-size: 2rem; font-weight: 750; color: #0f172a; }
.admin-product-form-card { border-radius: 1.25rem; }
.product-upload-zone { padding: 1rem; border: 1px dashed #94a3b8; border-radius: 1rem; background: #f8fafc; }
.product-upload-preview { display: grid; grid-template-columns: repeat(auto-fill, minmax(8rem, 1fr)); gap: .75rem; }
.product-upload-preview-item { position: relative; overflow: hidden; border: 1px solid #dbe2ea; border-radius: .75rem; background: #fff; }
.product-upload-preview-item img { width: 100%; height: 8rem; object-fit: cover; }
.product-upload-preview-item span { display: block; padding: .45rem .6rem; font-size: .75rem; font-weight: 650; color: #334155; }
.admin-product-table-card { overflow: hidden; border-radius: 1rem; }
.admin-product-table thead th { padding: 1rem; color: #475569; background: #f8fafc; white-space: nowrap; }
.admin-product-table tbody td { padding: 1rem; }
.admin-product-thumb { width: 3.75rem; height: 3.75rem; object-fit: contain; border: 1px solid #e2e8f0; border-radius: .65rem; background: #fff; }
.admin-product-thumb-empty { display: grid; place-items: center; color: #94a3b8; }
.admin-existing-images { display: flex; flex-wrap: wrap; gap: .75rem; }
.admin-existing-images a { display: block; border-radius: .75rem; transition: transform .15s ease, box-shadow .15s ease; }
.admin-existing-images a:hover { transform: translateY(-2px); box-shadow: 0 .5rem 1.25rem rgba(15, 23, 42, .14); }
.admin-existing-images img { width: 6rem; height: 6rem; object-fit: contain; border: 1px solid #dbe2ea; border-radius: .75rem; background: #fff; }
.order-card { border-radius: 1rem; }
.order-summary-card { top: 1.5rem; }
.order-qr-image { width: min(100%, 18rem); height: auto; image-rendering: crisp-edges; }
.qr-verification-card { border-radius: 1.2rem; }
.qr-scanner-stage {
  position: relative;
  overflow: hidden;
  aspect-ratio: 4 / 3;
  border-radius: 1rem;
  background: #020617;
}
.qr-scanner-stage video {
  width: 100%;
  height: 100%;
  object-fit: cover;
}
.qr-scanner-frame {
  position: absolute;
  inset: 14%;
  border: .22rem solid rgba(255, 255, 255, .92);
  border-radius: 1rem;
  box-shadow: 0 0 0 100vmax rgba(2, 6, 23, .35);
  pointer-events: none;
}
.qr-scanner-frame::before,
.qr-scanner-frame::after {
  position: absolute;
  width: 2.25rem;
  height: 2.25rem;
  border-color: #22c55e;
  content: "";
}
.qr-scanner-frame::before {
  top: -.22rem;
  left: -.22rem;
  border-top: .35rem solid #22c55e;
  border-left: .35rem solid #22c55e;
  border-radius: .85rem 0 0;
}
.qr-scanner-frame::after {
  right: -.22rem;
  bottom: -.22rem;
  border-right: .35rem solid #22c55e;
  border-bottom: .35rem solid #22c55e;
  border-radius: 0 0 .85rem;
}
.qr-verification-success .display-5 {
  display: inline-grid;
  width: 4.5rem;
  height: 4.5rem;
  place-items: center;
  border-radius: 50%;
  background: #dcfce7;
  color: #15803d;
}
.audit-table th, .audit-table td { padding: .75rem; vertical-align: top; }
.audit-details { max-width: 28rem; margin: 0; white-space: pre-wrap; overflow-wrap: anywhere; font-size: .75rem; }

@media (max-width: 767.98px) {
  .cart-item-grid { grid-template-columns: 6.5rem minmax(0, 1fr); gap: 1rem; align-items: start; }
  .cart-item-image-link { width: 6.5rem; height: 6.5rem; }
  .cart-item-price { grid-column: 2; min-width: 0; text-align: left; }
  .cart-list-header { display: none; }
  .product-gallery { grid-template-columns: 1fr; }
  .product-gallery-thumbnails { order: 2; flex-direction: row; overflow-x: auto; padding-bottom: .4rem; }
  .product-gallery-stage { min-height: 22rem; }
  .product-gallery-main-image { height: 22rem; }
}

@media (max-width: 575.98px) {
  .cart-item-grid { grid-template-columns: 1fr; }
  .cart-item-image-link { width: 100%; height: 13rem; }
  .cart-item-price { grid-column: auto; }
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

## `tests\SecureShop.Mvc.Tests\EmployeeOrdersControllerTests.cs`

Uzantı: `.cs`

```csharp
using Microsoft.AspNetCore.Mvc;
using SecureShop.Mvc.Controllers;
using SecureShop.Mvc.Http;
using SecureShop.Mvc.Models.Requests;
using SecureShop.Mvc.Models.Responses;
using SecureShop.Mvc.Models.ViewModels;
using SecureShop.Mvc.Services.Interfaces;

namespace SecureShop.Mvc.Tests;

public sealed class EmployeeOrdersControllerTests
{
    [Fact]
    public void VerifyGet_WithoutToken_ShowsScannerModel()
    {
        var controller = new EmployeeOrdersController(
            new UnusedOrderApiService());

        var result = Assert.IsType<ViewResult>(
            controller.Verify(null));
        var model = Assert.IsType<QrVerificationViewModel>(
            result.Model);

        Assert.Empty(model.Token);
        Assert.Null(model.Order);
    }

    [Fact]
    public void VerifyGet_WithToken_PreservesTokenForConfirmation()
    {
        var controller = new EmployeeOrdersController(
            new UnusedOrderApiService());

        var result = Assert.IsType<ViewResult>(
            controller.Verify("protected-token"));
        var model = Assert.IsType<QrVerificationViewModel>(
            result.Model);

        Assert.Equal("protected-token", model.Token);
        Assert.Null(model.Order);
    }

    private sealed class UnusedOrderApiService : IOrderApiService
    {
        public Task<ApiResponse<OrderResponse>> CreateAsync(
            CreateOrderRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ApiResponse<IReadOnlyList<OrderResponse>>>
            GetMineAsync(
                CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ApiResponse<OrderResponse>> GetMineAsync(
            string orderNumber,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ApiResponse<IReadOnlyList<OrderResponse>>>
            GetStaffAsync(
                CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ApiResponse<OrderResponse>> GetStaffAsync(
            string orderNumber,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ApiResponse<OrderResponse>> ApproveAsync(
            string orderNumber,
            ProcessOrderRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ApiResponse<OrderResponse>> MarkReadyAsync(
            string orderNumber,
            ProcessOrderRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ApiResponse<OrderResponse>> CancelAsync(
            string orderNumber,
            ProcessOrderRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ApiResponse<OrderResponse>> VerifyQrAsync(
            VerifyOrderQrRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
```

# 7. Kod Açıklaması

- `Verify(null)` boş token modelini view katmanına gönderir; bu model kamera arayüzünü seçer.
- `BarcodeDetector` yalnızca `qr_code` formatıyla oluşturulur.
- Kamera için `facingMode: environment` kullanılır; telefonun arka kamerası tercih edilir.
- QR adresinin origin değeri mevcut sayfayla, path değeri doğrulama route değeriyle birebir denetlenir.
- Token yeniden URL-encode edilerek aynı origin içindeki doğrulama adresine taşınır.
- Teslim durumu GET sırasında değişmez; çalışan tokenlı sayfada POST düğmesine basar.
- `pagehide` olayında tüm kamera track değerleri kapatılır.

# 8. API–MVC Veri Akışı

```text
Çalışan /employee/orders/verify adresini açar
        ↓
MVC EmployeeOrdersController.Verify(null)
        ↓
Verify.cshtml kamera arayüzünü ve qr-scanner.js dosyasını yükler
        ↓
Kamera müşterinin QR URL adresini okur
        ↓
JavaScript origin + path + token denetimi yapar
        ↓
/employee/orders/verify?token=... açılır
        ↓
Çalışan anti-forgery korumalı POST ile teslimi onaylar
        ↓
MVC IOrderApiService → API QR doğrulama → sipariş Delivered
```

# 9. Uygulama Sırası

1. API ve MVC süreçlerini başlat.
2. Employee veya Admin hesabıyla giriş yap.
3. `/employee/orders/verify` adresini aç.
4. `Kamerayı aç` düğmesine bas ve tarayıcı kamera iznini kabul et.
5. Müşterinin sipariş detayındaki QR kodunu çerçeve içine getir.
6. Sistem tokenlı onay ekranına geçtiğinde müşteriyi kontrol et.
7. `Teslimi doğrula ve siparişi tamamla` düğmesine bas.

# 10. Çalıştırma ve Test

Otomatik kontroller:

```text
Release build: başarılı, 0 uyarı, 0 hata
Unit + integration + MVC testleri: 33/33 başarılı
JavaScript sözdizimi: geçerli
EF pending model changes: yok
```

Gerçek HTTPS kontrolünde Employee login sonrasında şu sonuçlar doğrulandı:

```text
Token yok: HTTP 200 + kamera arayüzü + scanner script
Permissions-Policy: camera=(self), microphone=(), geolocation=()
Token var: hidden Token alanı + teslim onay formu
Token var: kamera arayüzü yüklenmiyor
```

# 11. Beklenen Sonuç

`https://localhost:7002/employee/orders/verify` artık “QR token bulunamadı”
uyarısında kalmaz. Çalışan kamerayı açarak QR kodunu tarar. Geçerli SecureShop
kodu algılandığında tokenlı teslim onay ekranı otomatik açılır. Harici origin, yanlış
route veya tokensız QR adresi kabul edilmez.

# 12. Yaygın Hatalar

- Kamera yalnızca HTTPS veya tarayıcının güvenilir kabul ettiği localhost üzerinde çalışır.
- Kamera izni reddedildiyse site ayarlarından izin yeniden açılmalıdır.
- BarcodeDetector desteklenmiyorsa telefonun ana kamera uygulaması QR URL adresini açabilir.
- Public tunnel ile üretilen QR, localhost origin değerindeki dahili tarayıcıda bilinçli olarak reddedilir.
- Telefon testi için QR URL adresi localhost değil, telefonun erişebildiği public HTTPS adresi olmalıdır.
- 7001/7002 exe dosyası kilitliyse eski `dotnet watch` süreçleri Ctrl+C ile kapatılıp yeniden başlatılmalıdır.

# 13. Tamamlama Kontrol Listesi

- [x] Token bulunmayan sayfada kamera tarayıcı gösteriliyor.
- [x] Arka kamera seçimi ve canlı tarama eklendi.
- [x] Görsel dosyasından QR okuma alternatifi eklendi.
- [x] Origin, route ve token doğrulaması eklendi.
- [x] Kamera kaynakları sayfa kapanırken temizleniyor.
- [x] Kamera izni Permissions-Policy ile aynı origin değerine sınırlandı.
- [x] Tokenlı teslim onay formu korunuyor.
- [x] Controller regresyon testleri eklendi.
- [x] Release build ve bütün testler başarılı.
- [x] Dosya yolları, uzantıları ve eksiksiz kodlar belgelendi.
