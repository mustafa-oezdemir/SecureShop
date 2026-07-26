# 1. Bu Adımın Amacı

Ürün detay adresinde teknik veritabanı kimliği olan GUID yerine kullanıcı
tarafından okunabilen ve ürünle ilişkilendirilebilen SKU değerini kullanmak.

```text
Eski adres:
https://localhost:7002/products/6fc1348c-0e47-44aa-98d5-26576c3631ac

Yeni adres:
https://localhost:7002/products/SSH-CAMERA-01
```

Eski GUID bağlantıları tamamen kaldırılmadı. Mevcut yer imlerinin ve eski
bağlantıların bozulmaması için GUID adresi ürünü bulur ve SKU adresine
`302 Found` ile yönlendirir.

# 2. Etkilenen Uygulama

```text
SecureShop.Api
SecureShop.Mvc
```

SQL Server şeması değişmedi. Yeni migration gerekmez. Ürün SKU değeri API
tarafından mevcut `Products` tablosundan okunur.

# 3. Bu Adımda Yapılacaklar

1. API ürün servisine SKU ile aktif ürün sorgusu eklemek.
2. Anonim `GET /api/products/by-sku/{sku}` endpoint'ini eklemek.
3. MVC typed API servisine SKU ile detay çağrısı eklemek.
4. MVC detay rotasını `/products/{sku}` yapmak.
5. Eski `/products/{id:guid}` adresini SKU adresine yönlendirmek.
6. Katalog, sepet ve admin ürün bağlantılarını SKU tabanlı üretmek.
7. Yeni ürün oluşturulduktan sonra SKU detay adresine yönlendirmek.
8. Build ve gerçek HTTP akışını doğrulamak.

# 4. CLI Komutları

Çalışma dizini: `D:\Code\ASP.NET\SecureShop`

```powershell
dotnet restore SecureShop.sln
dotnet build SecureShop.sln -c Release --no-restore
dotnet test SecureShop.sln -c Release --no-build
dotnet ef migrations has-pending-model-changes --project src\SecureShop.Api\SecureShop.Api.csproj --startup-project src\SecureShop.Api\SecureShop.Api.csproj --configuration Release --no-build
```

Terminal 1 — çalışma dizini:
`D:\Code\ASP.NET\SecureShop\src\SecureShop.Api`

```powershell
dotnet watch run --launch-profile https
```

Terminal 2 — çalışma dizini:
`D:\Code\ASP.NET\SecureShop\src\SecureShop.Mvc`

```powershell
dotnet watch run --launch-profile https
```

API `https://localhost:7001`, MVC `https://localhost:7002` adresinde çalışır.
Route metadata değişiklikleri hot reload ile yüklenmezse ilgili iki
`dotnet watch` terminalinde `Ctrl+R` kullanılmalıdır.

# 5. Güncel Proje Yapısı

```text
src/
├── SecureShop.Api/
│   ├── Controllers/
│   │   └── ProductsController.cs
│   └── Features/
│       └── Products/
│           ├── IProductService.cs
│           └── ProductService.cs
└── SecureShop.Mvc/
    ├── Controllers/
    │   ├── AdminProductsController.cs
    │   └── ProductsController.cs
    ├── Services/
    │   ├── Interfaces/
    │   │   └── IProductApiService.cs
    │   └── Api/
    │       └── ProductApiService.cs
    └── Views/
        ├── AdminProducts/
        │   └── Index.cshtml
        ├── Cart/
        │   └── Index.cshtml
        └── Products/
            └── Index.cshtml
```

# 6. Dosya Bazında Eksiksiz Kodlar

## `src/SecureShop.Api/Features/Products/IProductService.cs`

Extension: `.cs`

```csharp
using SecureShop.Api.Contracts.Requests;
using SecureShop.Api.Contracts.Responses;

namespace SecureShop.Api.Features.Products;

public interface IProductService
{
    Task<IReadOnlyList<ProductResponse>> GetPublicAsync(CancellationToken cancellationToken);
    Task<ProductResponse?> GetPublicByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ProductResponse?> GetPublicBySkuAsync(string sku, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProductResponse>> GetManagementAsync(CancellationToken cancellationToken);
    Task<ProductResponse?> GetManagementByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<CategoryOptionResponse>> GetCategoryOptionsAsync(CancellationToken cancellationToken);
    Task<ProductMutationResult> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken);
    Task<ProductMutationResult> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken);
    Task<ProductMutationResult> SetStatusAsync(Guid id, SetProductStatusRequest request, CancellationToken cancellationToken);
}
```

## `src/SecureShop.Api/Features/Products/ProductService.cs`

Extension: `.cs`

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

    public Task<ProductResponse?> GetPublicBySkuAsync(
        string sku,
        CancellationToken cancellationToken) =>
        GetBySkuAsync(sku, activeOnly: true, cancellationToken);

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

        for (var index = 0; index < request.Images.Count; index++)
        {
            var image = request.Images[index];

            product.AddImage(
                image.ImageUrl,
                image.AltText,
                index,
                isPrimary: index == 0);
        }

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
            .Include(product => product.Images)
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
            .Include(item => item.Images)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        return product is null ? null : Map(product);
    }

    private async Task<ProductResponse?> GetBySkuAsync(
        string sku,
        bool activeOnly,
        CancellationToken cancellationToken)
    {
        var normalizedSku = sku.Trim().ToUpperInvariant();
        var query = _dbContext.Products.AsNoTracking();

        if (activeOnly)
        {
            query = query.Where(product =>
                product.IsActive && product.Category.IsActive);
        }

        var product = await query
            .Include(item => item.Category)
            .Include(item => item.Images)
            .SingleOrDefaultAsync(
                item => item.Sku == normalizedSku,
                cancellationToken);

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
            product.Images
                .OrderBy(image => image.SortOrder)
                .Select(image => new ProductImageResponse(
                    image.Id,
                    image.ImageUrl,
                    image.AltText,
                    image.SortOrder,
                    image.IsPrimary))
                .ToList(),
            product.CreatedAtUtc,
            product.UpdatedAtUtc,
            Convert.ToBase64String(product.RowVersion));
}
```

## `src/SecureShop.Api/Controllers/ProductsController.cs`

Extension: `.cs`

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

    [AllowAnonymous]
    [HttpGet("by-sku/{sku}")]
    public async Task<ActionResult<ProductResponse>> GetPublicBySku(
        string sku,
        CancellationToken cancellationToken)
    {
        var product = await _productService.GetPublicBySkuAsync(
            sku,
            cancellationToken);

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
    [Authorize(Policy = AppPolicies.AdminOnly)]
    public async Task<ActionResult<ProductResponse>> Create(
        CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _productService.CreateAsync(request, cancellationToken);
        return ToActionResult(result, created: true);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AppPolicies.AdminOnly)]
    public async Task<ActionResult<ProductResponse>> Update(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _productService.UpdateAsync(id, request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = AppPolicies.AdminOnly)]
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

## `src/SecureShop.Mvc/Controllers/ProductsController.cs`

Extension: `.cs`

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

    [HttpGet("{sku}")]
    public async Task<IActionResult> Details(
        string sku,
        CancellationToken cancellationToken)
    {
        var result = await _productApiService.GetProductBySkuAsync(
            sku,
            cancellationToken);

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

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> DetailsById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _productApiService.GetProductAsync(
            id,
            cancellationToken);

        if (result.StatusCode == HttpStatusCode.NotFound)
        {
            return NotFound();
        }

        if (!result.IsSuccess || result.Data is null)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
            return RedirectToAction(nameof(Index));
        }

        return RedirectToAction(
            nameof(Details),
            new
            {
                sku = result.Data.Sku
            });
    }
}
```

## `src/SecureShop.Mvc/Services/Interfaces/IProductApiService.cs`

Extension: `.cs`

```csharp
using SecureShop.Mvc.Http;
using SecureShop.Mvc.Models.Requests;
using SecureShop.Mvc.Models.Responses;

namespace SecureShop.Mvc.Services.Interfaces;

public interface IProductApiService
{
    Task<ApiResponse<IReadOnlyList<ProductResponse>>> GetProductsAsync(
        CancellationToken cancellationToken = default);

    Task<ApiResponse<ProductResponse>> GetProductAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<ProductResponse>> GetProductBySkuAsync(
        string sku,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<IReadOnlyList<CategoryOptionResponse>>> GetCategoryOptionsAsync(
        CancellationToken cancellationToken = default);

    Task<ApiResponse<ProductResponse>> CreateProductAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<IReadOnlyList<ProductResponse>>> GetManagementProductsAsync(
        CancellationToken cancellationToken = default);

    Task<ApiResponse<ProductResponse>> GetManagementProductAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<ProductResponse>> UpdateProductAsync(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<ProductResponse>> SetProductStatusAsync(
        Guid id,
        SetProductStatusRequest request,
        CancellationToken cancellationToken = default);
}
```

## `src/SecureShop.Mvc/Services/Api/ProductApiService.cs`

Extension: `.cs`

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SecureShop.Mvc.Http;
using SecureShop.Mvc.Models.Requests;
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

    public async Task<ApiResponse<ProductResponse>> GetProductBySkuAsync(
        string sku,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var encodedSku = Uri.EscapeDataString(sku.Trim());
            using var response = await _httpClient.GetAsync(
                $"api/products/by-sku/{encodedSku}",
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return ApiResponse<ProductResponse>.Failure(
                    response.StatusCode,
                    "Ürün bulunamadı.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return ApiResponse<ProductResponse>.Failure(
                    response.StatusCode,
                    "Ürün API'den alınamadı.");
            }

            var product = await response.Content
                .ReadFromJsonAsync<ProductResponse>(
                    cancellationToken: cancellationToken);

            return product is null
                ? ApiResponse<ProductResponse>.Failure(
                    HttpStatusCode.BadGateway,
                    "API geçerli bir ürün response'u döndürmedi.")
                : ApiResponse<ProductResponse>.Success(
                    response.StatusCode,
                    product);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                exception,
                "SKU ile ürün detay API isteği tamamlanamadı.");

            return ApiResponse<ProductResponse>.Failure(
                HttpStatusCode.ServiceUnavailable,
                "SecureShop API hizmetine ulaşılamıyor.");
        }
        catch (JsonException exception)
        {
            _logger.LogError(
                exception,
                "SKU ile ürün detay API response'u okunamadı.");

            return ApiResponse<ProductResponse>.Failure(
                HttpStatusCode.BadGateway,
                "API ürün response formatı geçersiz.");
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return ApiResponse<ProductResponse>.Failure(
                HttpStatusCode.GatewayTimeout,
                "API isteği zaman aşımına uğradı.");
        }
    }

    public async Task<ApiResponse<IReadOnlyList<CategoryOptionResponse>>> GetCategoryOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(
                "api/products/category-options",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ApiResponse<IReadOnlyList<CategoryOptionResponse>>.Failure(
                    response.StatusCode,
                    "Kategori seçenekleri API'den alınamadı.");
            }

            var categories = await response.Content
                .ReadFromJsonAsync<List<CategoryOptionResponse>>(
                    cancellationToken: cancellationToken);

            return categories is null
                ? ApiResponse<IReadOnlyList<CategoryOptionResponse>>.Failure(
                    HttpStatusCode.BadGateway,
                    "API geçerli kategori seçenekleri döndürmedi.")
                : ApiResponse<IReadOnlyList<CategoryOptionResponse>>.Success(
                    response.StatusCode,
                    categories);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                exception,
                "Kategori seçenekleri API isteği tamamlanamadı.");

            return ApiResponse<IReadOnlyList<CategoryOptionResponse>>.Failure(
                HttpStatusCode.ServiceUnavailable,
                "SecureShop API hizmetine ulaşılamıyor.");
        }
        catch (JsonException exception)
        {
            _logger.LogError(
                exception,
                "Kategori seçenekleri API response'u okunamadı.");

            return ApiResponse<IReadOnlyList<CategoryOptionResponse>>.Failure(
                HttpStatusCode.BadGateway,
                "API kategori response formatı geçersiz.");
        }
    }

    public async Task<ApiResponse<ProductResponse>> CreateProductAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                "api/products",
                request,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var message = await ReadProblemDetailAsync(
                    response,
                    cancellationToken);

                return ApiResponse<ProductResponse>.Failure(
                    response.StatusCode,
                    message ?? "Ürün oluşturulamadı.");
            }

            var product = await response.Content
                .ReadFromJsonAsync<ProductResponse>(
                    cancellationToken: cancellationToken);

            return product is null
                ? ApiResponse<ProductResponse>.Failure(
                    HttpStatusCode.BadGateway,
                    "API geçerli ürün response'u döndürmedi.")
                : ApiResponse<ProductResponse>.Success(
                    response.StatusCode,
                    product);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                exception,
                "Ürün oluşturma API isteği tamamlanamadı.");

            return ApiResponse<ProductResponse>.Failure(
                HttpStatusCode.ServiceUnavailable,
                "SecureShop API hizmetine ulaşılamıyor.");
        }
        catch (JsonException exception)
        {
            _logger.LogError(
                exception,
                "Ürün oluşturma API response'u okunamadı.");

            return ApiResponse<ProductResponse>.Failure(
                HttpStatusCode.BadGateway,
                "API ürün response formatı geçersiz.");
        }
    }

    public Task<ApiResponse<IReadOnlyList<ProductResponse>>> GetManagementProductsAsync(
        CancellationToken cancellationToken = default) =>
        GetProductListAsync(
            "api/products/management",
            "Yönetim ürün listesi alınamadı.",
            cancellationToken);

    public Task<ApiResponse<ProductResponse>> GetManagementProductAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        GetProductResponseAsync(
            new HttpRequestMessage(
                HttpMethod.Get,
                $"api/products/management/{id:D}"),
            "Yönetim ürün bilgisi alınamadı.",
            cancellationToken);

    public Task<ApiResponse<ProductResponse>> UpdateProductAsync(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default) =>
        GetProductResponseAsync(
            new HttpRequestMessage(
                HttpMethod.Put,
                $"api/products/{id:D}")
            {
                Content = JsonContent.Create(request)
            },
            "Ürün güncellenemedi.",
            cancellationToken);

    public Task<ApiResponse<ProductResponse>> SetProductStatusAsync(
        Guid id,
        SetProductStatusRequest request,
        CancellationToken cancellationToken = default) =>
        GetProductResponseAsync(
            new HttpRequestMessage(
                HttpMethod.Patch,
                $"api/products/{id:D}/status")
            {
                Content = JsonContent.Create(request)
            },
            "Ürün durumu güncellenemedi.",
            cancellationToken);

    private async Task<ApiResponse<IReadOnlyList<ProductResponse>>> GetProductListAsync(
        string requestUri,
        string fallbackError,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(
                requestUri,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ApiResponse<IReadOnlyList<ProductResponse>>.Failure(
                    response.StatusCode,
                    fallbackError);
            }

            var products = await response.Content
                .ReadFromJsonAsync<List<ProductResponse>>(
                    cancellationToken: cancellationToken);

            return products is null
                ? ApiResponse<IReadOnlyList<ProductResponse>>.Failure(
                    HttpStatusCode.BadGateway,
                    "API geçerli ürün listesi döndürmedi.")
                : ApiResponse<IReadOnlyList<ProductResponse>>.Success(
                    response.StatusCode,
                    products);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                exception,
                "Yönetim ürün listesi API isteği tamamlanamadı.");

            return ApiResponse<IReadOnlyList<ProductResponse>>.Failure(
                HttpStatusCode.ServiceUnavailable,
                "SecureShop API hizmetine ulaşılamıyor.");
        }
        catch (JsonException exception)
        {
            _logger.LogError(
                exception,
                "Yönetim ürün listesi response'u okunamadı.");

            return ApiResponse<IReadOnlyList<ProductResponse>>.Failure(
                HttpStatusCode.BadGateway,
                "API ürün response formatı geçersiz.");
        }
    }

    private async Task<ApiResponse<ProductResponse>> GetProductResponseAsync(
        HttpRequestMessage request,
        string fallbackError,
        CancellationToken cancellationToken)
    {
        using (request)
        {
            try
            {
                using var response = await _httpClient.SendAsync(
                    request,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return ApiResponse<ProductResponse>.Failure(
                        response.StatusCode,
                        await ReadProblemDetailAsync(
                            response,
                            cancellationToken)
                            ?? fallbackError);
                }

                var product = await response.Content
                    .ReadFromJsonAsync<ProductResponse>(
                        cancellationToken: cancellationToken);

                return product is null
                    ? ApiResponse<ProductResponse>.Failure(
                        HttpStatusCode.BadGateway,
                        "API geçerli ürün response'u döndürmedi.")
                    : ApiResponse<ProductResponse>.Success(
                        response.StatusCode,
                        product);
            }
            catch (HttpRequestException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Ürün yönetimi API isteği tamamlanamadı.");

                return ApiResponse<ProductResponse>.Failure(
                    HttpStatusCode.ServiceUnavailable,
                    "SecureShop API hizmetine ulaşılamıyor.");
            }
            catch (JsonException exception)
            {
                _logger.LogError(
                    exception,
                    "Ürün yönetimi API response'u okunamadı.");

                return ApiResponse<ProductResponse>.Failure(
                    HttpStatusCode.BadGateway,
                    "API ürün response formatı geçersiz.");
            }
        }
    }

    private static async Task<string?> ReadProblemDetailAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var problem = await response.Content
                .ReadFromJsonAsync<ProblemDetails>(
                    cancellationToken: cancellationToken);

            return problem?.Detail;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
```

## `src/SecureShop.Mvc/Controllers/AdminProductsController.cs`

Extension: `.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureShop.Mvc.Models.Requests;
using SecureShop.Mvc.Models.ViewModels;
using SecureShop.Mvc.Security;
using SecureShop.Mvc.Services.Interfaces;

namespace SecureShop.Mvc.Controllers;

[Authorize(Roles = AppRoles.Admin)]
[Route("admin/products")]
public sealed class AdminProductsController : Controller
{
    private const int MaximumImageCount = 10;

    private readonly IProductApiService _productApiService;
    private readonly IProductImageStorage _imageStorage;

    public AdminProductsController(
        IProductApiService productApiService,
        IProductImageStorage imageStorage)
    {
        _productApiService = productApiService;
        _imageStorage = imageStorage;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken)
    {
        var result = await _productApiService
            .GetManagementProductsAsync(cancellationToken);

        return View(new AdminProductListViewModel
        {
            Products = result.Data ?? [],
            ErrorMessage = result.ErrorMessage
        });
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create(
        CancellationToken cancellationToken)
    {
        var model = new CreateProductViewModel();

        await LoadCategoriesAsync(model, cancellationToken);

        return View(model);
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(55 * 1024 * 1024)]
    public async Task<IActionResult> Create(
        CreateProductViewModel model,
        CancellationToken cancellationToken)
    {
        if (model.Images.Count is < 1 or > MaximumImageCount)
        {
            ModelState.AddModelError(
                nameof(model.Images),
                $"1 ile {MaximumImageCount} arasında fotoğraf seçin.");
        }

        if (!ModelState.IsValid)
        {
            await LoadCategoriesAsync(model, cancellationToken);
            return View(model);
        }

        var storageResult = await _imageStorage.SaveAsync(
            model.Images,
            model.Sku,
            model.Name,
            cancellationToken);

        if (!storageResult.Succeeded)
        {
            ModelState.AddModelError(
                nameof(model.Images),
                storageResult.ErrorMessage
                    ?? "Ürün fotoğrafları kaydedilemedi.");

            await LoadCategoriesAsync(model, cancellationToken);
            return View(model);
        }

        var request = new CreateProductRequest(
            model.CategoryId,
            model.Name.Trim(),
            model.Sku.Trim(),
            string.IsNullOrWhiteSpace(model.Description)
                ? null
                : model.Description.Trim(),
            model.Price,
            model.StockQuantity,
            storageResult.Images);

        var result = await _productApiService.CreateProductAsync(
            request,
            cancellationToken);

        if (!result.IsSuccess || result.Data is null)
        {
            await _imageStorage.DeleteAsync(
                storageResult.FolderName,
                cancellationToken);

            ModelState.AddModelError(
                string.Empty,
                result.ErrorMessage ?? "Ürün oluşturulamadı.");

            await LoadCategoriesAsync(model, cancellationToken);
            return View(model);
        }

        TempData["SuccessMessage"] =
            "Ürün ve fotoğrafları başarıyla oluşturuldu.";

        return RedirectToAction(
            "Details",
            "Products",
            new
            {
                sku = result.Data.Sku
            });
    }

    [HttpGet("{id:guid}/edit")]
    public async Task<IActionResult> Edit(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _productApiService
            .GetManagementProductAsync(id, cancellationToken);

        if (!result.IsSuccess || result.Data is null)
        {
            TempData["ErrorMessage"] =
                result.ErrorMessage ?? "Ürün bulunamadı.";

            return RedirectToAction(nameof(Index));
        }

        var product = result.Data;
        var model = new EditProductViewModel
        {
            Id = product.Id,
            CategoryId = product.CategoryId,
            Name = product.Name,
            Sku = product.Sku,
            Description = product.Description,
            Price = product.Price,
            StockQuantity = product.StockQuantity,
            RowVersion = product.RowVersion,
            Images = product.Images
        };

        await LoadCategoriesAsync(model, cancellationToken);

        return View(model);
    }

    [HttpPost("{id:guid}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        Guid id,
        EditProductViewModel model,
        CancellationToken cancellationToken)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            await LoadCategoriesAsync(model, cancellationToken);
            return View(model);
        }

        var request = new UpdateProductRequest(
            model.CategoryId,
            model.Name.Trim(),
            model.Sku.Trim(),
            string.IsNullOrWhiteSpace(model.Description)
                ? null
                : model.Description.Trim(),
            model.Price,
            model.StockQuantity,
            model.RowVersion);

        var result = await _productApiService.UpdateProductAsync(
            id,
            request,
            cancellationToken);

        if (!result.IsSuccess || result.Data is null)
        {
            ModelState.AddModelError(
                string.Empty,
                result.ErrorMessage ?? "Ürün güncellenemedi.");

            await LoadCategoriesAsync(model, cancellationToken);
            return View(model);
        }

        TempData["SuccessMessage"] = "Ürün başarıyla güncellendi.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(
        Guid id,
        bool isActive,
        string rowVersion,
        CancellationToken cancellationToken)
    {
        var result = await _productApiService.SetProductStatusAsync(
            id,
            new SetProductStatusRequest(isActive, rowVersion),
            cancellationToken);

        TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
            result.IsSuccess
                ? isActive
                    ? "Ürün yeniden aktifleştirildi."
                    : "Ürün pasife alındı."
                : result.ErrorMessage ?? "Ürün durumu güncellenemedi.";

        return RedirectToAction(nameof(Index));
    }

    private async Task LoadCategoriesAsync(
        CreateProductViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await _productApiService
            .GetCategoryOptionsAsync(cancellationToken);

        model.Categories = result.Data ?? [];

        if (!result.IsSuccess)
        {
            ModelState.AddModelError(
                string.Empty,
                result.ErrorMessage
                    ?? "Kategori seçenekleri yüklenemedi.");
        }
    }

    private async Task LoadCategoriesAsync(
        EditProductViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await _productApiService
            .GetCategoryOptionsAsync(cancellationToken);

        model.Categories = result.Data ?? [];

        if (!result.IsSuccess)
        {
            ModelState.AddModelError(
                string.Empty,
                result.ErrorMessage
                    ?? "Kategori seçenekleri yüklenemedi.");
        }
    }
}
```

## `src/SecureShop.Mvc/Views/Products/Index.cshtml`

Extension: `.cshtml`

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
            var primaryImage = product.Images
                .OrderBy(image => image.SortOrder)
                .FirstOrDefault(image => image.IsPrimary)
                ?? product.Images.OrderBy(image => image.SortOrder).FirstOrDefault();

            <div class="col">
                <article class="card h-100 border-0 shadow-sm product-card">
                    @if (primaryImage is not null)
                    {
                        <a asp-action="Details" asp-route-sku="@product.Sku" class="product-card-image-link">
                            <img src="@primaryImage.ImageUrl"
                                 alt="@primaryImage.AltText"
                                 class="card-img-top product-card-image"
                                 loading="lazy" />
                        </a>
                    }
                    <div class="card-body">
                        <span class="badge bg-secondary">@product.CategoryName</span>
                        <h2 class="h5 mt-2">@product.Name</h2>
                        <p class="text-muted">SKU: @product.Sku</p>
                        <p class="fw-bold">@product.Price.ToString("N2") €</p>
                        <p>Stok: @product.StockQuantity</p>
                        <a asp-action="Details" asp-route-sku="@product.Sku" class="btn btn-primary">Detay</a>
                    </div>
                </article>
            </div>
        }
    </div>
}
```

## `src/SecureShop.Mvc/Views/Cart/Index.cshtml`

Extension: `.cshtml`

```cshtml
@model SecureShop.Mvc.Models.ViewModels.CartViewModel
@{
    ViewData["Title"] = "Sepetim";
    var cart = Model.Cart;
}

<div class="d-flex justify-content-between align-items-center mb-4">
    <div>
        <span class="text-uppercase text-primary fw-semibold small">Secure checkout</span>
        <h1 class="mb-0">Sepetim</h1>
    </div>
    <a asp-controller="Products" asp-action="Index" class="btn btn-outline-primary">
        Alışverişe devam et
    </a>
</div>

@if (TempData["SuccessMessage"] is string successMessage)
{
    <div class="alert alert-success" role="status">@successMessage</div>
}

@if (TempData["ErrorMessage"] is string operationError)
{
    <div class="alert alert-danger" role="alert">@operationError</div>
}

@if (!string.IsNullOrWhiteSpace(Model.ErrorMessage))
{
    <div class="alert alert-danger" role="alert">@Model.ErrorMessage</div>
}
else if (cart is null || cart.Items.Count == 0)
{
    <section class="card border-0 shadow-sm cart-empty-state">
        <div class="card-body text-center p-5">
            <div class="cart-empty-icon" aria-hidden="true">🛒</div>
            <h2 class="h4 mt-3">Sepetiniz henüz boş</h2>
            <p class="text-body-secondary">Ürün kataloğundan ilk ürününüzü ekleyin.</p>
            <a asp-controller="Products" asp-action="Index" class="btn btn-primary">
                Ürünleri keşfet
            </a>
        </div>
    </section>
}
else
{
    <div class="row g-4">
        <div class="col-lg-8">
            <div class="vstack gap-3">
                @foreach (var item in cart.Items)
                {
                    <article class="card border-0 shadow-sm cart-item-card">
                        <div class="card-body p-4">
                            <div class="row align-items-center g-3">
                                <div class="col-md">
                                    <a asp-controller="Products"
                                       asp-action="Details"
                                       asp-route-sku="@item.Sku"
                                       class="h5 text-decoration-none text-dark">
                                        @item.ProductName
                                    </a>
                                    <div class="text-body-secondary small">SKU: @item.Sku</div>
                                    <div class="fw-semibold mt-2">@item.UnitPrice.ToString("N2") € / adet</div>

                                    @if (!item.IsAvailable)
                                    {
                                        <div class="text-danger small mt-2">
                                            Bu ürün şu anda satışa uygun değil.
                                        </div>
                                    }
                                </div>
                                <div class="col-md-auto">
                                    <form asp-action="Update"
                                          asp-route-itemId="@item.Id"
                                          method="post"
                                          class="d-flex align-items-end gap-2">
                                        <div>
                                            <label for="quantity-@item.Id" class="form-label small">Adet</label>
                                            <input id="quantity-@item.Id"
                                                   name="Quantity"
                                                   type="number"
                                                   min="1"
                                                   max="@Math.Min(item.AvailableStock, 99)"
                                                   value="@item.Quantity"
                                                   class="form-control cart-quantity-input"
                                                   disabled="@(!item.IsAvailable)" />
                                        </div>
                                        <button type="submit"
                                                class="btn btn-outline-primary"
                                                disabled="@(!item.IsAvailable)">
                                            Güncelle
                                        </button>
                                    </form>
                                </div>
                                <div class="col-md-auto text-md-end">
                                    <div class="small text-body-secondary">Ara toplam</div>
                                    <div class="h5 mb-2">@item.LineTotal.ToString("N2") €</div>
                                    <form asp-action="Remove"
                                          asp-route-itemId="@item.Id"
                                          method="post">
                                        <button type="submit" class="btn btn-sm btn-link text-danger p-0">
                                            Sepetten çıkar
                                        </button>
                                    </form>
                                </div>
                            </div>
                        </div>
                    </article>
                }
            </div>
        </div>

        <div class="col-lg-4">
            <aside class="card border-0 shadow-sm cart-summary-card">
                <div class="card-body p-4">
                    <h2 class="h4">Sepet özeti</h2>
                    <div class="d-flex justify-content-between py-3 border-bottom">
                        <span>Toplam adet</span>
                        <strong>@cart.TotalQuantity</strong>
                    </div>
                    <div class="d-flex justify-content-between py-3">
                        <span>Toplam</span>
                        <strong class="h4 mb-0">@cart.TotalAmount.ToString("N2") €</strong>
                    </div>
                    <p class="small text-body-secondary">
                        Nihai fiyat ve stok sipariş oluşturulurken API tarafından tekrar doğrulanacaktır.
                    </p>
                    <button type="button" class="btn btn-primary w-100" disabled>
                        Sipariş adımında etkinleşecek
                    </button>
                    <form asp-action="Clear" method="post" class="mt-3 text-center">
                        <button type="submit" class="btn btn-link text-danger">
                            Sepeti temizle
                        </button>
                    </form>
                </div>
            </aside>
        </div>
    </div>
}
```

## `src/SecureShop.Mvc/Views/AdminProducts/Index.cshtml`

Extension: `.cshtml`

```cshtml
@model SecureShop.Mvc.Models.ViewModels.AdminProductListViewModel
@{
    ViewData["Title"] = "Ürün yönetimi";
}

<div class="d-flex flex-wrap justify-content-between align-items-center gap-3 mb-4">
    <div>
        <span class="text-uppercase text-primary fw-semibold small">Admin paneli</span>
        <h1 class="mb-0">Ürün yönetimi</h1>
    </div>
    <a asp-action="Create" class="btn btn-primary btn-lg">
        + Yeni ürün
    </a>
</div>

@if (TempData["SuccessMessage"] is string successMessage)
{
    <div class="alert alert-success" role="status">@successMessage</div>
}

@if (TempData["ErrorMessage"] is string operationError)
{
    <div class="alert alert-danger" role="alert">@operationError</div>
}

@if (!string.IsNullOrWhiteSpace(Model.ErrorMessage))
{
    <div class="alert alert-danger" role="alert">@Model.ErrorMessage</div>
}
else if (Model.Products.Count == 0)
{
    <div class="card border-0 shadow-sm">
        <div class="card-body p-5 text-center">
            <h2 class="h4">Henüz ürün yok</h2>
            <p class="text-body-secondary">İlk ürünü ve fotoğraflarını ekleyin.</p>
            <a asp-action="Create" class="btn btn-primary">Yeni ürün ekle</a>
        </div>
    </div>
}
else
{
    <div class="card border-0 shadow-sm admin-product-table-card">
        <div class="table-responsive">
            <table class="table align-middle mb-0 admin-product-table">
                <thead>
                    <tr>
                        <th>Ürün</th>
                        <th>Kategori</th>
                        <th>Fiyat</th>
                        <th>Stok</th>
                        <th>Durum</th>
                        <th class="text-end">İşlemler</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach (var product in Model.Products)
                    {
                        var primaryImage = product.Images
                            .OrderBy(image => image.SortOrder)
                            .FirstOrDefault(image => image.IsPrimary)
                            ?? product.Images.OrderBy(image => image.SortOrder).FirstOrDefault();

                        <tr>
                            <td>
                                <div class="d-flex align-items-center gap-3">
                                    @if (primaryImage is not null)
                                    {
                                        <img src="@primaryImage.ImageUrl"
                                             alt=""
                                             class="admin-product-thumb" />
                                    }
                                    else
                                    {
                                        <span class="admin-product-thumb admin-product-thumb-empty">—</span>
                                    }
                                    <div>
                                        <strong class="d-block">@product.Name</strong>
                                        <span class="text-body-secondary small">@product.Sku</span>
                                    </div>
                                </div>
                            </td>
                            <td>@product.CategoryName</td>
                            <td>@product.Price.ToString("N2") €</td>
                            <td>@product.StockQuantity</td>
                            <td>
                                <span class="badge @(product.IsActive ? "text-bg-success" : "text-bg-secondary")">
                                    @(product.IsActive ? "Aktif" : "Pasif")
                                </span>
                            </td>
                            <td>
                                <div class="d-flex justify-content-end flex-wrap gap-2">
                                    <a asp-controller="Products"
                                       asp-action="Details"
                                       asp-route-sku="@product.Sku"
                                       class="btn btn-sm btn-outline-secondary">
                                        Görüntüle
                                    </a>
                                    <a asp-action="Edit"
                                       asp-route-id="@product.Id"
                                       class="btn btn-sm btn-outline-primary">
                                        Düzenle
                                    </a>
                                    <form asp-action="SetStatus"
                                          asp-route-id="@product.Id"
                                          method="post">
                                        <input type="hidden" name="isActive" value="@(!product.IsActive)" />
                                        <input type="hidden" name="rowVersion" value="@product.RowVersion" />
                                        <button type="submit"
                                                class="btn btn-sm @(product.IsActive ? "btn-outline-danger" : "btn-outline-success")">
                                            @(product.IsActive ? "Pasife al" : "Aktifleştir")
                                        </button>
                                    </form>
                                </div>
                            </td>
                        </tr>
                    }
                </tbody>
            </table>
        </div>
    </div>
}
```

# 7. Kod Açıklaması

## API

`GetPublicBySkuAsync`, SKU'yu kırpar ve büyük harfe normalize eder. Sorgu
yalnızca aktif ürünü ve aktif kategoriyi kabul eder. Böylece pasif bir ürünün
SKU'su bilinerek detay verisine erişilemez.

API controller entity döndürmez; `ProductResponse` DTO üretir. Yeni endpoint
ürün bulunamazsa `404 Not Found`, bulunduğunda `200 OK` döndürür.

## MVC

MVC controller doğrudan SQL Server'a erişmez. SKU değerini typed
`IProductApiService` üzerinden API'ye gönderir. SKU URL parçası
`Uri.EscapeDataString` ile güvenli biçimde encode edilir.

Eski GUID rotası yalnızca geriye dönük uyumluluk içindir:

1. Ürünü eski ID ile API'den bulur.
2. Response içindeki güvenilir SKU'yu alır.
3. `/products/{sku}` adresine yönlendirir.

Ürün kartları, sepet ve admin liste bağlantıları artık `asp-route-sku`
kullanır. Düzenleme ve durum değiştirme işlemleri iç sistem kimliği olarak
GUID kullanmaya devam eder; yalnızca kullanıcıya açık detay URL'si değişir.

# 8. API–MVC Veri Akışı

```text
Ürün kartındaki /products/SSH-CAMERA-01 bağlantısı
    ↓
SecureShop.Mvc ProductsController.Details("SSH-CAMERA-01")
    ↓
IProductApiService.GetProductBySkuAsync
    ↓
HttpClient
    ↓
GET https://localhost:7001/api/products/by-sku/SSH-CAMERA-01
    ↓
SecureShop.Api ProductsController.GetPublicBySku
    ↓
IProductService.GetPublicBySkuAsync
    ↓
AppDbContext
    ↓
SQL Server
    ↓
ProductResponse JSON
    ↓
Razor ürün detay görünümü
```

# 9. Uygulama Sırası

1. `IProductService` sözleşmesini genişlet.
2. `ProductService` SKU sorgusunu uygula.
3. API endpoint'ini ekle.
4. MVC `IProductApiService` sözleşmesini genişlet.
5. `ProductApiService` HTTP çağrısını uygula.
6. MVC ürün controller rotalarını güncelle.
7. Katalog, sepet ve admin bağlantılarını değiştir.
8. Yeni ürün sonrası yönlendirmeyi SKU yap.
9. Build ve HTTP testlerini çalıştır.

# 10. Çalıştırma ve Test

İki uygulama ayrı terminallerde çalıştırılır. Ardından:

```powershell
curl.exe -k -i https://localhost:7001/api/products/by-sku/SSH-CAMERA-01
curl.exe -k -i https://localhost:7002/products/SSH-CAMERA-01
curl.exe -k -i https://localhost:7002/products/6fc1348c-0e47-44aa-98d5-26576c3631ac
```

Gerçekleştirilen izole Release doğrulamasında:

```text
API SKU endpoint'i: 200 OK
MVC SKU detay sayfası: 200 OK
Eski GUID sayfası: 302 Found
Location: /products/SSH-CAMERA-01
```

# 11. Beklenen Sonuç

Kullanıcı katalogdan `4K Aksiyon Kamerası` ürününe bastığında tarayıcı adresi:

```text
https://localhost:7002/products/SSH-CAMERA-01
```

olur. Sayfada ürün adı, `SKU: SSH-CAMERA-01`, dört ürün fotoğrafı, fiyat,
stok ve sepete ekleme alanı gösterilir.

# 12. Yaygın Hatalar

## Yeni SKU adresi `404` döndürüyor

Çalışan Debug uygulaması eski route metadata'sını taşıyor olabilir. API ve MVC
`dotnet watch` terminallerinde `Ctrl+R` kullanın veya iki süreci temiz biçimde
yeniden başlatın.

## MVC API'ye bağlanamıyor

`SecureShop.Api` çalışmıyorsa MVC logunda `localhost:7001 connection refused`
görülür. API ve MVC aynı anda, ayrı terminallerde çalışmalıdır.

## Build sırasında `MSB3027` veya `MSB3021`

Aynı proje için ikinci bir `dotnet run` ya da `dotnet watch` süreci
çalışıyordur. İlgili fazladan süreci kapatın; her proje için yalnızca bir
watcher bırakın.

## SKU ile pasif ürün açılmıyor

Bu beklenen güvenlik davranışıdır. Public SKU endpoint'i yalnızca aktif ürün
ve aktif kategori döndürür.

# 13. Tamamlama Kontrol Listesi

```text
[x] CLI komutları doğru klasörde çalıştırıldı
[x] Dosyalar doğru projede oluşturuldu
[x] Web API hatasız derlendi
[x] MVC uygulaması hatasız derlendi
[x] MVC, Web API'ye başarıyla bağlandı
[x] MVC doğrudan veritabanına erişmiyor
[x] Web API veritabanı işlemini başarıyla gerçekleştirdi
[x] SKU API endpoint'i HTTP 200 döndürdü
[x] SKU MVC detay sayfası HTTP 200 döndürdü
[x] Eski GUID URL'si SKU URL'sine yönlendirildi
[x] İstenen özellik doğrulandı
```
