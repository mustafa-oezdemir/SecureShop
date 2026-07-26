# SecureShop Ürün Yönetimi – Web API

## 1. Adımın amacı

Bu adımda ürün yönetiminin Web API çekirdeği oluşturulmuştur. Public ürün
listeleme ve detay endpoint'leri anonim kullanıma açılmış; oluşturma, güncelleme,
aktif/pasif yapma ve yönetim sorguları yalnızca `Admin` veya `Employee`
rollerinin karşılayabildiği `StaffOnly` policy'siyle korunmuştur.

MVC ürün ekranları bu adımın kapsamına alınmamıştır. MVC, sonraki alt adımda bu
API sözleşmelerini `HttpClient` üzerinden kullanacaktır.

## 2. Etkilenen uygulamalar

```text
SecureShop.Api : Değişti
SecureShop.Mvc : Değişmedi
SQL Server     : Şema değişmedi
```

Yeni NuGet paketi veya Entity Framework Core migration'ı gerekmez.

## 3. Güvenlik ve tasarım kararları

- Ürün entity'leri doğrudan API response'u olarak döndürülmez.
- Request ve response DTO'ları kullanılır.
- Fiyat, stok, SKU ve kategori bilgileri API tarafında doğrulanır.
- Yönetim endpoint'leri `AppPolicies.StaffOnly` ile korunur.
- Public sorgular yalnızca aktif ürünleri ve aktif kategorileri döndürür.
- Fiziksel silme yapılmaz; ürün aktif veya pasif duruma getirilir.
- SKU normalize edilerek büyük harfe çevrilir ve benzersizliği doğrulanır.
- Veritabanındaki unique index yarış koşullarına karşı da yakalanır.
- SQL Server `rowversion` değeri optimistic concurrency token olarak kullanılır.
- Eski veya geçersiz `rowVersion` ile yapılan güncelleme reddedilir.
- Başka bir kullanıcının değiştirdiği kaydın üzerine sessizce yazılmaz.
- Kategori kimliği istemciden gelse de kategori varlığı ve aktifliği API'de doğrulanır.
- Controller yalnızca HTTP eşlemesi yapar; iş kuralları `ProductService` içindedir.

## 4. API endpoint'leri

| Metot | Adres | Yetki | Açıklama |
|---|---|---|---|
| GET | `/api/products` | Anonim | Aktif ürünleri listeler |
| GET | `/api/products/{id}` | Anonim | Aktif ürün detayını döndürür |
| GET | `/api/products/management` | StaffOnly | Aktif/pasif bütün ürünleri listeler |
| GET | `/api/products/management/{id}` | StaffOnly | Yönetim ürün detayını döndürür |
| GET | `/api/products/category-options` | StaffOnly | Aktif kategori seçeneklerini döndürür |
| POST | `/api/products` | StaffOnly | Ürün oluşturur |
| PUT | `/api/products/{id}` | StaffOnly | Ürünü günceller |
| PATCH | `/api/products/{id}/status` | StaffOnly | Ürünü aktif/pasif yapar |

## 5. HTTP sonuçları

```text
200 OK           : Liste, detay, güncelleme veya durum değişikliği başarılı
201 Created      : Ürün oluşturuldu
400 Bad Request  : Validation, kategori veya rowVersion geçersiz
401 Unauthorized : Authentication cookie yok/geçersiz
403 Forbidden    : Kullanıcı StaffOnly policy'sini karşılamıyor
404 Not Found    : Ürün bulunamadı
409 Conflict     : SKU çakışması veya concurrency conflict
```

## 6. Dosya yolları ve uzantıları

| No | Durum | Dosya yolu | Uzantı |
|---:|---|---|---|
| 1 | Yeni | `src/SecureShop.Api/Contracts/Requests/CreateProductRequest.cs` | `.cs` |
| 2 | Yeni | `src/SecureShop.Api/Contracts/Requests/UpdateProductRequest.cs` | `.cs` |
| 3 | Yeni | `src/SecureShop.Api/Contracts/Requests/SetProductStatusRequest.cs` | `.cs` |
| 4 | Yeni | `src/SecureShop.Api/Contracts/Responses/ProductResponse.cs` | `.cs` |
| 5 | Yeni | `src/SecureShop.Api/Contracts/Responses/CategoryOptionResponse.cs` | `.cs` |
| 6 | Yeni | `src/SecureShop.Api/Features/Products/ProductMutationResult.cs` | `.cs` |
| 7 | Yeni | `src/SecureShop.Api/Features/Products/IProductService.cs` | `.cs` |
| 8 | Yeni | `src/SecureShop.Api/Features/Products/ProductService.cs` | `.cs` |
| 9 | Yeni | `src/SecureShop.Api/Controllers/ProductsController.cs` | `.cs` |
| 10 | Değişti | `src/SecureShop.Api/Program.cs` | `.cs` |

## 7. CLI komutları

Çalışma dizini:

```text
D:\Code\ASP.NET\SecureShop
```

```powershell
dotnet restore .\SecureShop.sln
dotnet build .\SecureShop.sln -c Release
```

Pending model değişikliği kontrolü:

```powershell
dotnet ef migrations has-pending-model-changes --project .\src\SecureShop.Api\SecureShop.Api.csproj --startup-project .\src\SecureShop.Api\SecureShop.Api.csproj -- --environment Development
```

API'yi çalıştırma:

```powershell
dotnet watch run --project .\src\SecureShop.Api\SecureShop.Api.csproj --launch-profile https
```

## 8. Örnek request'ler

### Ürün oluşturma

```json
{
  "categoryId": "AKTIF-KATEGORI-GUID",
  "name": "Güvenli Klavye",
  "sku": "KEYBOARD-001",
  "description": "Mekanik klavye",
  "price": 129.90,
  "stockQuantity": 20
}
```

### Ürün güncelleme

```json
{
  "categoryId": "AKTIF-KATEGORI-GUID",
  "name": "Güvenli Klavye V2",
  "sku": "KEYBOARD-001",
  "description": "Güncellenmiş açıklama",
  "price": 139.90,
  "stockQuantity": 18,
  "rowVersion": "API-RESPONSE-ICINDEKI-BASE64-DEGER"
}
```

### Ürünü pasif yapma

```json
{
  "isActive": false,
  "rowVersion": "API-RESPONSE-ICINDEKI-BASE64-DEGER"
}
```

## 9. Veri akışı

```text
HTTP Request
    ↓
ProductsController
    ↓
IProductService
    ↓
ProductService
    ↓
AppDbContext
    ↓
SQL Server
    ↓
ProductResponse / ProblemDetails
```

## 10. Doğrulama sonuçları

```text
Release build                     : Başarılı
Uyarı                             : 0
Hata                              : 0
Pending EF model değişikliği      : Yok
GET /api/products                 : 200
GET /api/products/management      : 401 (anonim kullanıcı)
```

## 11. Önemli not

Ürün oluşturmak için veritabanında en az bir aktif kategori bulunmalıdır.
Kategori yoksa API `400 Bad Request` döndürür. Kategori yönetimi veya kategori
seed işlemi bu adıma bilinçli olarak eklenmemiştir.

## 12. Tamamlama kontrol listesi

```text
[x] Public ürün listeleme
[x] Public ürün detay
[x] StaffOnly yönetim listeleme ve detay
[x] Ürün oluşturma
[x] Ürün güncelleme
[x] Aktif/pasif yapma
[x] Server-side validation
[x] SKU benzersizlik kontrolü
[x] Kategori varlık ve aktiflik kontrolü
[x] RowVersion concurrency koruması
[x] Entity doğrudan response olarak dönmüyor
[x] Fiziksel silme yapılmıyor
[x] Release build başarılı
[x] Migration gerekmiyor
[ ] MVC ürün listeleme ve yönetim ekranları
```

## 13. Resmî kaynaklar

- [EF Core concurrency conflicts](https://learn.microsoft.com/en-us/ef/core/saving/concurrency)
- [ASP.NET Core model validation](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/validation?view=aspnetcore-10.0)
- [ASP.NET Core policy-based authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies?view=aspnetcore-10.0)

## 14. Dosya bazında eksiksiz kodlar

### `src/SecureShop.Api/Contracts/Requests/CreateProductRequest.cs`

Dosya uzantısı: `.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace SecureShop.Api.Contracts.Requests;

public sealed class CreateProductRequest
{
    [Required]
    public Guid CategoryId { get; init; }

    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [StringLength(64, MinimumLength = 1)]
    [RegularExpression("^[A-Za-z0-9._-]+$")]
    public string Sku { get; init; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; init; }

    [Range(typeof(decimal), "0", "9999999999999999.99")]
    public decimal Price { get; init; }

    [Range(0, int.MaxValue)]
    public int StockQuantity { get; init; }
}

```

### `src/SecureShop.Api/Contracts/Requests/UpdateProductRequest.cs`

Dosya uzantısı: `.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace SecureShop.Api.Contracts.Requests;

public sealed class UpdateProductRequest
{
    [Required]
    public Guid CategoryId { get; init; }

    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [StringLength(64, MinimumLength = 1)]
    [RegularExpression("^[A-Za-z0-9._-]+$")]
    public string Sku { get; init; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; init; }

    [Range(typeof(decimal), "0", "9999999999999999.99")]
    public decimal Price { get; init; }

    [Range(0, int.MaxValue)]
    public int StockQuantity { get; init; }

    [Required]
    public string RowVersion { get; init; } = string.Empty;
}

```

### `src/SecureShop.Api/Contracts/Requests/SetProductStatusRequest.cs`

Dosya uzantısı: `.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace SecureShop.Api.Contracts.Requests;

public sealed class SetProductStatusRequest
{
    public bool IsActive { get; init; }

    [Required]
    public string RowVersion { get; init; } = string.Empty;
}

```

### `src/SecureShop.Api/Contracts/Responses/ProductResponse.cs`

Dosya uzantısı: `.cs`

```csharp
namespace SecureShop.Api.Contracts.Responses;

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

### `src/SecureShop.Api/Contracts/Responses/CategoryOptionResponse.cs`

Dosya uzantısı: `.cs`

```csharp
namespace SecureShop.Api.Contracts.Responses;

public sealed record CategoryOptionResponse(
    Guid Id,
    string Name);

```

### `src/SecureShop.Api/Features/Products/ProductMutationResult.cs`

Dosya uzantısı: `.cs`

```csharp
using SecureShop.Api.Contracts.Responses;

namespace SecureShop.Api.Features.Products;

public enum ProductMutationStatus
{
    Succeeded,
    NotFound,
    CategoryNotFound,
    DuplicateSku,
    InvalidRowVersion,
    ConcurrencyConflict
}

public sealed record ProductMutationResult(
    ProductMutationStatus Status,
    ProductResponse? Product = null);

```

### `src/SecureShop.Api/Features/Products/IProductService.cs`

Dosya uzantısı: `.cs`

```csharp
using SecureShop.Api.Contracts.Requests;
using SecureShop.Api.Contracts.Responses;

namespace SecureShop.Api.Features.Products;

public interface IProductService
{
    Task<IReadOnlyList<ProductResponse>> GetPublicAsync(CancellationToken cancellationToken);
    Task<ProductResponse?> GetPublicByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProductResponse>> GetManagementAsync(CancellationToken cancellationToken);
    Task<ProductResponse?> GetManagementByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<CategoryOptionResponse>> GetCategoryOptionsAsync(CancellationToken cancellationToken);
    Task<ProductMutationResult> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken);
    Task<ProductMutationResult> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken);
    Task<ProductMutationResult> SetStatusAsync(Guid id, SetProductStatusRequest request, CancellationToken cancellationToken);
}

```

### `src/SecureShop.Api/Features/Products/ProductService.cs`

Dosya uzantısı: `.cs`

```csharp
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SecureShop.Api.Contracts.Requests;
using SecureShop.Api.Contracts.Responses;
using SecureShop.Api.Data;
using SecureShop.Api.Domain.Entities;

namespace SecureShop.Api.Features.Products;

public sealed class ProductService : IProductService
{
    private readonly AppDbContext _dbContext;

    public ProductService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<IReadOnlyList<ProductResponse>> GetPublicAsync(
        CancellationToken cancellationToken) =>
        GetListAsync(activeOnly: true, cancellationToken);

    public Task<ProductResponse?> GetPublicByIdAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        GetByIdAsync(id, activeOnly: true, cancellationToken);

    public Task<IReadOnlyList<ProductResponse>> GetManagementAsync(
        CancellationToken cancellationToken) =>
        GetListAsync(activeOnly: false, cancellationToken);

    public Task<ProductResponse?> GetManagementByIdAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        GetByIdAsync(id, activeOnly: false, cancellationToken);

    public async Task<IReadOnlyList<CategoryOptionResponse>> GetCategoryOptionsAsync(
        CancellationToken cancellationToken)
    {
        return await _dbContext.Categories
            .AsNoTracking()
            .Where(category => category.IsActive)
            .OrderBy(category => category.Name)
            .Select(category => new CategoryOptionResponse(category.Id, category.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProductMutationResult> CreateAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        if (!await CategoryExistsAsync(request.CategoryId, cancellationToken))
        {
            return new(ProductMutationStatus.CategoryNotFound);
        }

        var normalizedSku = request.Sku.Trim().ToUpperInvariant();
        if (await SkuExistsAsync(normalizedSku, null, cancellationToken))
        {
            return new(ProductMutationStatus.DuplicateSku);
        }

        var product = new Product(
            request.CategoryId,
            request.Name,
            request.Sku,
            request.Price,
            request.StockQuantity,
            request.Description);

        _dbContext.Products.Add(product);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return new(ProductMutationStatus.DuplicateSku);
        }

        return new(
            ProductMutationStatus.Succeeded,
            await GetManagementByIdAsync(product.Id, cancellationToken));
    }

    public async Task<ProductMutationResult> UpdateAsync(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var product = await _dbContext.Products
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (product is null)
        {
            return new(ProductMutationStatus.NotFound);
        }

        if (!TryDecodeRowVersion(request.RowVersion, out var rowVersion))
        {
            return new(ProductMutationStatus.InvalidRowVersion);
        }

        if (!await CategoryExistsAsync(request.CategoryId, cancellationToken))
        {
            return new(ProductMutationStatus.CategoryNotFound);
        }

        var normalizedSku = request.Sku.Trim().ToUpperInvariant();
        if (await SkuExistsAsync(normalizedSku, id, cancellationToken))
        {
            return new(ProductMutationStatus.DuplicateSku);
        }

        _dbContext.Entry(product).Property(item => item.RowVersion).OriginalValue = rowVersion;
        product.ChangeCategory(request.CategoryId);
        product.SetName(request.Name);
        product.SetSku(request.Sku);
        product.SetDescription(request.Description);
        product.SetPrice(request.Price);
        product.SetStockQuantity(request.StockQuantity);

        return await SaveMutationAsync(product, cancellationToken);
    }

    public async Task<ProductMutationResult> SetStatusAsync(
        Guid id,
        SetProductStatusRequest request,
        CancellationToken cancellationToken)
    {
        var product = await _dbContext.Products
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (product is null)
        {
            return new(ProductMutationStatus.NotFound);
        }

        if (!TryDecodeRowVersion(request.RowVersion, out var rowVersion))
        {
            return new(ProductMutationStatus.InvalidRowVersion);
        }

        _dbContext.Entry(product).Property(item => item.RowVersion).OriginalValue = rowVersion;

        if (request.IsActive)
        {
            product.Activate();
        }
        else
        {
            product.Deactivate();
        }

        return await SaveMutationAsync(product, cancellationToken);
    }

    private async Task<IReadOnlyList<ProductResponse>> GetListAsync(
        bool activeOnly,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Products.AsNoTracking();
        if (activeOnly)
        {
            query = query.Where(product => product.IsActive && product.Category.IsActive);
        }

        var products = await query
            .Include(product => product.Category)
            .OrderBy(product => product.Name)
            .ToListAsync(cancellationToken);

        return products.Select(Map).ToList();
    }

    private async Task<ProductResponse?> GetByIdAsync(
        Guid id,
        bool activeOnly,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Products.AsNoTracking();
        if (activeOnly)
        {
            query = query.Where(product => product.IsActive && product.Category.IsActive);
        }

        var product = await query
            .Include(item => item.Category)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        return product is null ? null : Map(product);
    }

    private async Task<ProductMutationResult> SaveMutationAsync(
        Product product,
        CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(ProductMutationStatus.ConcurrencyConflict);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return new(ProductMutationStatus.DuplicateSku);
        }

        return new(
            ProductMutationStatus.Succeeded,
            await GetManagementByIdAsync(product.Id, cancellationToken));
    }

    private Task<bool> CategoryExistsAsync(Guid categoryId, CancellationToken cancellationToken) =>
        _dbContext.Categories.AnyAsync(
            category => category.Id == categoryId && category.IsActive,
            cancellationToken);

    private Task<bool> SkuExistsAsync(string sku, Guid? excludedId, CancellationToken cancellationToken) =>
        _dbContext.Products.AnyAsync(
            product => product.Sku == sku && (!excludedId.HasValue || product.Id != excludedId.Value),
            cancellationToken);

    private static bool TryDecodeRowVersion(string value, out byte[] rowVersion)
    {
        try
        {
            rowVersion = Convert.FromBase64String(value);
            return rowVersion.Length == 8;
        }
        catch (FormatException)
        {
            rowVersion = [];
            return false;
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };

    private static ProductResponse Map(Product product) =>
        new(
            product.Id,
            product.CategoryId,
            product.Category.Name,
            product.Name,
            product.Sku,
            product.Description,
            product.Price,
            product.StockQuantity,
            product.IsActive,
            product.CreatedAtUtc,
            product.UpdatedAtUtc,
            Convert.ToBase64String(product.RowVersion));
}

```

### `src/SecureShop.Api/Controllers/ProductsController.cs`

Dosya uzantısı: `.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureShop.Api.Contracts.Requests;
using SecureShop.Api.Contracts.Responses;
using SecureShop.Api.Features.Products;
using SecureShop.Api.Security.Policies;

namespace SecureShop.Api.Controllers;

[ApiController]
[Route("api/products")]
[Authorize(Policy = AppPolicies.StaffOnly)]
public sealed class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductResponse>>> GetPublic(
        CancellationToken cancellationToken) =>
        Ok(await _productService.GetPublicAsync(cancellationToken));

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductResponse>> GetPublicById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var product = await _productService.GetPublicByIdAsync(id, cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpGet("management")]
    public async Task<ActionResult<IReadOnlyList<ProductResponse>>> GetManagement(
        CancellationToken cancellationToken) =>
        Ok(await _productService.GetManagementAsync(cancellationToken));

    [HttpGet("management/{id:guid}")]
    public async Task<ActionResult<ProductResponse>> GetManagementById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var product = await _productService.GetManagementByIdAsync(id, cancellationToken);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpGet("category-options")]
    public async Task<ActionResult<IReadOnlyList<CategoryOptionResponse>>> GetCategoryOptions(
        CancellationToken cancellationToken) =>
        Ok(await _productService.GetCategoryOptionsAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ProductResponse>> Create(
        CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _productService.CreateAsync(request, cancellationToken);
        return ToActionResult(result, created: true);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProductResponse>> Update(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _productService.UpdateAsync(id, request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<ProductResponse>> SetStatus(
        Guid id,
        SetProductStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _productService.SetStatusAsync(id, request, cancellationToken);
        return ToActionResult(result);
    }

    private ActionResult<ProductResponse> ToActionResult(
        ProductMutationResult result,
        bool created = false)
    {
        if (result.Status == ProductMutationStatus.Succeeded && result.Product is not null)
        {
            return created
                ? CreatedAtAction(nameof(GetManagementById), new { id = result.Product.Id }, result.Product)
                : Ok(result.Product);
        }

        return result.Status switch
        {
            ProductMutationStatus.NotFound => NotFound(CreateProblem("Ürün bulunamadı.")),
            ProductMutationStatus.CategoryNotFound => BadRequest(CreateProblem("Aktif kategori bulunamadı.")),
            ProductMutationStatus.DuplicateSku => Conflict(CreateProblem("SKU başka bir ürün tarafından kullanılıyor.")),
            ProductMutationStatus.InvalidRowVersion => BadRequest(CreateProblem("RowVersion geçersiz.")),
            ProductMutationStatus.ConcurrencyConflict => Conflict(CreateProblem("Ürün başka bir kullanıcı tarafından değiştirildi. Sayfayı yenileyin.")),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    private static ProblemDetails CreateProblem(string detail) =>
        new() { Detail = detail };
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

