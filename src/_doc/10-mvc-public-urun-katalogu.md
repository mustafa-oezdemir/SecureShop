# SecureShop MVC Public Ürün Kataloğu

## 1. Amaç

MVC uygulamasının veritabanına erişmeden public ürün listesini ve detayını SecureShop.Api üzerinden alıp Razor View'larda göstermesi sağlandı.

## 2. Etkilenen uygulamalar

```text
SecureShop.Api : Değişmedi
SecureShop.Mvc : Değişti
SQL Server     : MVC doğrudan erişmiyor
```

## 3. Yapılanlar

- API response sözleşmesinin MVC karşılığı oluşturuldu.
- `IProductApiService` ve typed `HttpClient` eklendi.
- 404, API bağlantısı, JSON ve timeout hataları kontrollü işlendi.
- `ProductsController` yalnızca API çağrısı ve ViewModel hazırlıyor.
- Public liste ve detay Razor View'ları oluşturuldu.
- Navigasyona Ürünler bağlantısı eklendi.
- Fiyat ve stok API response'undan gösteriliyor; MVC hesaplama yapmıyor.

## 4. Veri akışı

```text
Tarayıcı → ProductsController → IProductApiService → HttpClient
→ SecureShop.Api /api/products → AppDbContext → SQL Server
→ ProductResponse → Razor View
```

## 5. Rotalar

| MVC adresi | API adresi | Yetki |
|---|---|---|
| `GET /products` | `GET /api/products` | Anonim |
| `GET /products/{id}` | `GET /api/products/{id}` | Anonim |

## 6. Dosya yolları ve uzantıları

| No | Durum | Dosya yolu | Uzantı |
|---:|---|---|---|
| 1 | Yeni | `src/SecureShop.Mvc/Models/Responses/ProductResponse.cs` | `.cs` |
| 2 | Yeni | `src/SecureShop.Mvc/Services/Interfaces/IProductApiService.cs` | `.cs` |
| 3 | Yeni | `src/SecureShop.Mvc/Services/Api/ProductApiService.cs` | `.cs` |
| 4 | Yeni | `src/SecureShop.Mvc/Models/ViewModels/ProductListViewModel.cs` | `.cs` |
| 5 | Yeni | `src/SecureShop.Mvc/Controllers/ProductsController.cs` | `.cs` |
| 6 | Yeni | `src/SecureShop.Mvc/Views/Products/Index.cshtml` | `.cshtml` |
| 7 | Yeni | `src/SecureShop.Mvc/Views/Products/Details.cshtml` | `.cshtml` |
| 8 | Değişti | `src/SecureShop.Mvc/Program.cs` | `.cs` |
| 9 | Değişti | `src/SecureShop.Mvc/Views/Shared/_Layout.cshtml` | `.cshtml` |

## 7. Çalıştırma

```powershell
dotnet build .\SecureShop.sln -c Release
dotnet watch run --project .\src\SecureShop.Api\SecureShop.Api.csproj --launch-profile https
dotnet watch run --project .\src\SecureShop.Mvc\SecureShop.Mvc.csproj --launch-profile https
```

Tarayıcı: `https://localhost:7002/products`

## 8. Doğrulama

```text
Solution Release build : başarılı, 0 uyarı, 0 hata
MVC GET /products      : 200
MVC doğrudan DB erişimi: yok
```

## 9. Kontrol listesi

```text
[x] Typed Product API client
[x] Public ürün listesi
[x] Public ürün detayı
[x] Kontrollü API hata yönetimi
[x] Razor otomatik HTML encoding
[x] MVC'de EF Core/SQL Server yok
[x] Build ve birlikte çalışma testi başarılı
[ ] MVC StaffOnly ürün yönetim ekranları
```

## 10. Dosya bazında eksiksiz kodlar

### `src/SecureShop.Mvc/Models/Responses/ProductResponse.cs`

Dosya uzantısı: `.cs`

```csharp
namespace SecureShop.Mvc.Models.Responses;

public sealed record ProductResponse(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string Name,
    string Sku,
    string? Description,
    decimal Price,
    int StockQuantity,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    string RowVersion);

```

### `src/SecureShop.Mvc/Services/Interfaces/IProductApiService.cs`

Dosya uzantısı: `.cs`

```csharp
using SecureShop.Mvc.Http;
using SecureShop.Mvc.Models.Responses;

namespace SecureShop.Mvc.Services.Interfaces;

public interface IProductApiService
{
    Task<ApiResponse<IReadOnlyList<ProductResponse>>> GetProductsAsync(
        CancellationToken cancellationToken = default);

    Task<ApiResponse<ProductResponse>> GetProductAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}

```

### `src/SecureShop.Mvc/Services/Api/ProductApiService.cs`

Dosya uzantısı: `.cs`

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SecureShop.Mvc.Http;
using SecureShop.Mvc.Models.Responses;
using SecureShop.Mvc.Services.Interfaces;

namespace SecureShop.Mvc.Services.Api;

public sealed class ProductApiService : IProductApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ProductApiService> _logger;

    public ProductApiService(HttpClient httpClient, ILogger<ProductApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ApiResponse<IReadOnlyList<ProductResponse>>> GetProductsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("api/products", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiResponse<IReadOnlyList<ProductResponse>>.Failure(
                    response.StatusCode, "Ürünler API'den alınamadı.");
            }

            var products = await response.Content.ReadFromJsonAsync<List<ProductResponse>>(
                cancellationToken: cancellationToken);

            return products is null
                ? ApiResponse<IReadOnlyList<ProductResponse>>.Failure(
                    HttpStatusCode.BadGateway, "API geçerli bir ürün listesi döndürmedi.")
                : ApiResponse<IReadOnlyList<ProductResponse>>.Success(response.StatusCode, products);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Ürün listesi API isteği tamamlanamadı.");
            return ApiResponse<IReadOnlyList<ProductResponse>>.Failure(
                HttpStatusCode.ServiceUnavailable, "SecureShop API hizmetine ulaşılamıyor.");
        }
        catch (JsonException exception)
        {
            _logger.LogError(exception, "Ürün listesi API response'u okunamadı.");
            return ApiResponse<IReadOnlyList<ProductResponse>>.Failure(
                HttpStatusCode.BadGateway, "API ürün response formatı geçersiz.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ApiResponse<IReadOnlyList<ProductResponse>>.Failure(
                HttpStatusCode.GatewayTimeout, "API isteği zaman aşımına uğradı.");
        }
    }

    public async Task<ApiResponse<ProductResponse>> GetProductAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync($"api/products/{id:D}", cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return ApiResponse<ProductResponse>.Failure(response.StatusCode, "Ürün bulunamadı.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return ApiResponse<ProductResponse>.Failure(response.StatusCode, "Ürün API'den alınamadı.");
            }

            var product = await response.Content.ReadFromJsonAsync<ProductResponse>(
                cancellationToken: cancellationToken);

            return product is null
                ? ApiResponse<ProductResponse>.Failure(
                    HttpStatusCode.BadGateway, "API geçerli bir ürün response'u döndürmedi.")
                : ApiResponse<ProductResponse>.Success(response.StatusCode, product);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Ürün detay API isteği tamamlanamadı.");
            return ApiResponse<ProductResponse>.Failure(
                HttpStatusCode.ServiceUnavailable, "SecureShop API hizmetine ulaşılamıyor.");
        }
        catch (JsonException exception)
        {
            _logger.LogError(exception, "Ürün detay API response'u okunamadı.");
            return ApiResponse<ProductResponse>.Failure(
                HttpStatusCode.BadGateway, "API ürün response formatı geçersiz.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ApiResponse<ProductResponse>.Failure(
                HttpStatusCode.GatewayTimeout, "API isteği zaman aşımına uğradı.");
        }
    }
}

```

### `src/SecureShop.Mvc/Models/ViewModels/ProductListViewModel.cs`

Dosya uzantısı: `.cs`

```csharp
using SecureShop.Mvc.Models.Responses;

namespace SecureShop.Mvc.Models.ViewModels;

public sealed class ProductListViewModel
{
    public IReadOnlyList<ProductResponse> Products { get; init; } = [];
    public string? ErrorMessage { get; init; }
}

```

### `src/SecureShop.Mvc/Controllers/ProductsController.cs`

Dosya uzantısı: `.cs`

```csharp
using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureShop.Mvc.Models.ViewModels;
using SecureShop.Mvc.Services.Interfaces;

namespace SecureShop.Mvc.Controllers;

[AllowAnonymous]
[Route("products")]
public sealed class ProductsController : Controller
{
    private readonly IProductApiService _productApiService;

    public ProductsController(IProductApiService productApiService)
    {
        _productApiService = productApiService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var result = await _productApiService.GetProductsAsync(cancellationToken);
        return View(new ProductListViewModel
        {
            Products = result.Data ?? [],
            ErrorMessage = result.ErrorMessage
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var result = await _productApiService.GetProductAsync(id, cancellationToken);
        if (result.StatusCode == HttpStatusCode.NotFound)
        {
            return NotFound();
        }

        if (!result.IsSuccess || result.Data is null)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
            return RedirectToAction(nameof(Index));
        }

        return View(result.Data);
    }
}

```

### `src/SecureShop.Mvc/Views/Products/Index.cshtml`

Dosya uzantısı: `.cshtml`

```cshtml
@model SecureShop.Mvc.Models.ViewModels.ProductListViewModel
@{ ViewData["Title"] = "Ürünler"; }

<h1>Ürünler</h1>

@if (!string.IsNullOrWhiteSpace(Model.ErrorMessage))
{
    <div class="alert alert-danger" role="alert">@Model.ErrorMessage</div>
}
else if (Model.Products.Count == 0)
{
    <div class="alert alert-info">Henüz görüntülenecek aktif ürün bulunmuyor.</div>
}
else
{
    <div class="row row-cols-1 row-cols-md-3 g-4">
        @foreach (var product in Model.Products)
        {
            <div class="col">
                <article class="card h-100">
                    <div class="card-body">
                        <span class="badge bg-secondary">@product.CategoryName</span>
                        <h2 class="h5 mt-2">@product.Name</h2>
                        <p class="text-muted">SKU: @product.Sku</p>
                        <p class="fw-bold">@product.Price.ToString("N2") €</p>
                        <p>Stok: @product.StockQuantity</p>
                        <a asp-action="Details" asp-route-id="@product.Id" class="btn btn-primary">Detay</a>
                    </div>
                </article>
            </div>
        }
    </div>
}

```

### `src/SecureShop.Mvc/Views/Products/Details.cshtml`

Dosya uzantısı: `.cshtml`

```cshtml
@model SecureShop.Mvc.Models.Responses.ProductResponse
@{ ViewData["Title"] = Model.Name; }

<article class="card">
    <div class="card-body">
        <span class="badge bg-secondary">@Model.CategoryName</span>
        <h1 class="mt-2">@Model.Name</h1>
        <p class="text-muted">SKU: @Model.Sku</p>
        <p>@(Model.Description ?? "Bu ürün için açıklama bulunmuyor.")</p>
        <dl class="row">
            <dt class="col-sm-3">Fiyat</dt><dd class="col-sm-9">@Model.Price.ToString("N2") €</dd>
            <dt class="col-sm-3">Stok</dt><dd class="col-sm-9">@Model.StockQuantity</dd>
        </dl>
        <a asp-action="Index" class="btn btn-outline-secondary">Ürünlere dön</a>
    </div>
</article>

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

### `src/SecureShop.Mvc/Views/Shared/_Layout.cshtml`

Dosya uzantısı: `.cshtml`

```cshtml
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@ViewData["Title"] - SecureShop.Mvc</title>
    <script type="importmap"></script>
    <link rel="stylesheet" href="~/lib/bootstrap/dist/css/bootstrap.min.css" />
    <link rel="stylesheet" href="~/css/site.css" asp-append-version="true" />
    <link rel="stylesheet" href="~/SecureShop.Mvc.styles.css" asp-append-version="true" />
</head>
<body>
    <header>
        <nav class="navbar navbar-expand-sm navbar-toggleable-sm navbar-light bg-white border-bottom box-shadow mb-3">
            <div class="container-fluid">
                <a class="navbar-brand" asp-area="" asp-controller="Home" asp-action="Index">SecureShop.Mvc</a>
                <button class="navbar-toggler" type="button" data-bs-toggle="collapse" data-bs-target=".navbar-collapse" aria-controls="navbarSupportedContent"
                        aria-expanded="false" aria-label="Toggle navigation">
                    <span class="navbar-toggler-icon"></span>
                </button>
                <div class="navbar-collapse collapse d-sm-inline-flex justify-content-between">
                    <ul class="navbar-nav flex-grow-1">
                        <li class="nav-item">
                            <a class="nav-link text-dark" asp-area="" asp-controller="Home" asp-action="Index">Home</a>
                        </li>
                        <li class="nav-item">
                            <a class="nav-link text-dark" asp-area="" asp-controller="Home" asp-action="Privacy">Privacy</a>
                        </li>
                        <li class="nav-item">
                            <a class="nav-link text-dark" asp-controller="Products" asp-action="Index">Ürünler</a>
                        </li>
                    </ul>
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
            &copy; 2026 - SecureShop.Mvc - <a asp-area="" asp-controller="Home" asp-action="Privacy">Privacy</a>
        </div>
    </footer>
    <script src="~/lib/jquery/dist/jquery.min.js"></script>
    <script src="~/lib/bootstrap/dist/js/bootstrap.bundle.min.js"></script>
    <script src="~/js/site.js" asp-append-version="true"></script>
    @await RenderSectionAsync("Scripts", required: false)
</body>
</html>

```

