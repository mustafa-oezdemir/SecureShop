# 1. Bu Adımın Amacı

Admin ürün düzenleme adresinde GUID yerine SKU kullanmak ve mevcut ürüne aynı
düzenleme formundan güvenli biçimde birden fazla yeni fotoğraf eklemek.

```text
Eski:
/admin/products/6fc1348c-0e47-44aa-98d5-26576c3631ac/edit

Yeni:
/admin/products/SSH-CAMERA-01/edit
```

Mevcut fotoğraflar tıklanabilir bağlantı olarak gösterilir. Admin yeni
fotoğrafları seçip ürün bilgileriyle birlikte tek işlemde kaydedebilir.

# 2. Etkilenen Uygulama

```text
SecureShop.Api
SecureShop.Mvc
```

SQL Server şeması değişmediği için migration oluşturulmadı. Yeni fotoğraf
kayıtları mevcut `ProductImages` tablosuna yalnızca API üzerinden eklenir.

# 3. Bu Adımda Yapılacaklar

1. Admin ürün bilgisini SKU ile alan API endpoint'i eklemek.
2. Admin edit GET ve POST rotalarını SKU tabanlı yapmak.
3. Eski GUID edit adresini SKU adresine yönlendirmek.
4. Edit ViewModel'e çoklu dosya alanı eklemek.
5. Mevcut SKU klasörüne güvenli ek dosya kaydı yapmak.
6. Ürün bilgileri ve yeni görsel metadata'sını API'de tek transaction ile
   güncellemek.
7. Toplam 10 fotoğraf sınırını hem MVC hem API tarafında doğrulamak.
8. API başarısız olduğunda yalnızca yeni yazılan dosyaları geri almak.
9. Mevcut görselleri tıklanabilir bağlantı olarak göstermek.

# 4. CLI Komutları

Çalışma dizini: `D:\Code\ASP.NET\SecureShop`

```powershell
dotnet restore SecureShop.sln
dotnet build SecureShop.sln -c Release --no-restore
dotnet test SecureShop.sln -c Release --no-build
dotnet ef migrations has-pending-model-changes --project src\SecureShop.Api\SecureShop.Api.csproj --startup-project src\SecureShop.Api\SecureShop.Api.csproj --configuration Release --no-build
```

Terminal 1 — `D:\Code\ASP.NET\SecureShop\src\SecureShop.Api`

```powershell
dotnet watch run --launch-profile https
```

Terminal 2 — `D:\Code\ASP.NET\SecureShop\src\SecureShop.Mvc`

```powershell
dotnet watch run --launch-profile https
```

Route ve form metadata değişikliklerinden sonra iki watcher için bir kez
`Ctrl+R` gerekebilir.

# 5. Güncel Proje Yapısı

```text
src/
├── SecureShop.Api/
│   ├── Contracts/Requests/
│   │   └── UpdateProductRequest.cs
│   ├── Controllers/
│   │   └── ProductsController.cs
│   └── Features/Products/
│       ├── IProductService.cs
│       ├── ProductMutationResult.cs
│       └── ProductService.cs
└── SecureShop.Mvc/
    ├── Controllers/
    │   └── AdminProductsController.cs
    ├── Models/
    │   ├── Requests/
    │   │   └── UpdateProductRequest.cs
    │   └── ViewModels/
    │       └── EditProductViewModel.cs
    ├── Services/
    │   ├── Api/
    │   │   └── ProductApiService.cs
    │   ├── Interfaces/
    │   │   ├── IProductApiService.cs
    │   │   └── IProductImageStorage.cs
    │   └── Storage/
    │       └── ProductImageStorage.cs
    ├── Views/AdminProducts/
    │   ├── Edit.cshtml
    │   └── Index.cshtml
    └── wwwroot/css/
        └── site.css
```

# 6. Dosya Bazında Eksiksiz Kodlar

## `src/SecureShop.Api/Contracts/Requests/UpdateProductRequest.cs`

Extension: `.cs`

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

    [Range(
        typeof(decimal),
        "0",
        "9999999999999999.99",
        ParseLimitsInInvariantCulture = true)]
    public decimal Price { get; init; }

    [Range(0, int.MaxValue)]
    public int StockQuantity { get; init; }

    [Required]
    public string RowVersion { get; init; } = string.Empty;

    [MaxLength(10)]
    public IReadOnlyList<CreateProductImageRequest> Images { get; init; } = [];
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

    [HttpGet("management/by-sku/{sku}")]
    public async Task<ActionResult<ProductResponse>> GetManagementBySku(
        string sku,
        CancellationToken cancellationToken)
    {
        var product = await _productService.GetManagementBySkuAsync(
            sku,
            cancellationToken);

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
            ProductMutationStatus.TooManyImages => BadRequest(CreateProblem("Bir üründe en fazla 10 fotoğraf olabilir.")),
            ProductMutationStatus.InvalidRowVersion => BadRequest(CreateProblem("RowVersion geçersiz.")),
            ProductMutationStatus.ConcurrencyConflict => Conflict(CreateProblem("Ürün başka bir kullanıcı tarafından değiştirildi. Sayfayı yenileyin.")),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    private static ProblemDetails CreateProblem(string detail) =>
        new() { Detail = detail };
}
```

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
    Task<ProductResponse?> GetManagementBySkuAsync(string sku, CancellationToken cancellationToken);
    Task<IReadOnlyList<CategoryOptionResponse>> GetCategoryOptionsAsync(CancellationToken cancellationToken);
    Task<ProductMutationResult> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken);
    Task<ProductMutationResult> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken);
    Task<ProductMutationResult> SetStatusAsync(Guid id, SetProductStatusRequest request, CancellationToken cancellationToken);
}
```

## `src/SecureShop.Api/Features/Products/ProductMutationResult.cs`

Extension: `.cs`

```csharp
using SecureShop.Api.Contracts.Responses;

namespace SecureShop.Api.Features.Products;

public enum ProductMutationStatus
{
    Succeeded,
    NotFound,
    CategoryNotFound,
    DuplicateSku,
    TooManyImages,
    InvalidRowVersion,
    ConcurrencyConflict
}

public sealed record ProductMutationResult(
    ProductMutationStatus Status,
    ProductResponse? Product = null);
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

    public Task<ProductResponse?> GetManagementBySkuAsync(
        string sku,
        CancellationToken cancellationToken) =>
        GetBySkuAsync(sku, activeOnly: false, cancellationToken);

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
            .Include(item => item.Images)
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

        if (product.Images.Count + request.Images.Count > 10)
        {
            return new(ProductMutationStatus.TooManyImages);
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

        var nextSortOrder = product.Images.Count == 0
            ? 0
            : product.Images.Max(image => image.SortOrder) + 1;

        for (var index = 0; index < request.Images.Count; index++)
        {
            var image = request.Images[index];

            product.AddImage(
                image.ImageUrl,
                image.AltText,
                nextSortOrder + index,
                isPrimary: product.Images.Count == 0 && index == 0);
        }

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

## `src/SecureShop.Mvc/Models/Requests/UpdateProductRequest.cs`

Extension: `.cs`

```csharp
namespace SecureShop.Mvc.Models.Requests;

public sealed record UpdateProductRequest(
    Guid CategoryId,
    string Name,
    string Sku,
    string? Description,
    decimal Price,
    int StockQuantity,
    string RowVersion,
    IReadOnlyList<CreateProductImageRequest> Images);
```

## `src/SecureShop.Mvc/Models/ViewModels/EditProductViewModel.cs`

Extension: `.cs`

```csharp
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using SecureShop.Mvc.Models.Responses;

namespace SecureShop.Mvc.Models.ViewModels;

public sealed class EditProductViewModel
{
    public Guid Id { get; set; }

    [Required(ErrorMessage = "Kategori seçin.")]
    [Display(Name = "Kategori")]
    public Guid CategoryId { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 2)]
    [Display(Name = "Ürün adı")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(64, MinimumLength = 1)]
    [RegularExpression(
        "^[A-Za-z0-9._-]+$",
        ErrorMessage = "SKU yalnızca harf, rakam, nokta, alt çizgi ve tire içerebilir.")]
    public string Sku { get; set; } = string.Empty;

    [StringLength(2000)]
    [Display(Name = "Açıklama")]
    public string? Description { get; set; }

    [Range(
        typeof(decimal),
        "0",
        "9999999999999999.99",
        ParseLimitsInInvariantCulture = true)]
    [Display(Name = "Fiyat")]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue)]
    [Display(Name = "Stok adedi")]
    public int StockQuantity { get; set; }

    [Required]
    public string RowVersion { get; set; } = string.Empty;

    public IReadOnlyList<ProductImageResponse> Images { get; set; } = [];

    [Display(Name = "Yeni fotoğraflar")]
    public IReadOnlyList<IFormFile> NewImages { get; set; } = [];

    public IReadOnlyList<CategoryOptionResponse> Categories { get; set; } = [];
}
```

## `src/SecureShop.Mvc/Services/Interfaces/IProductImageStorage.cs`

Extension: `.cs`

```csharp
using Microsoft.AspNetCore.Http;
using SecureShop.Mvc.Models.Requests;
using SecureShop.Mvc.Services.Storage;

namespace SecureShop.Mvc.Services.Interfaces;

public interface IProductImageStorage
{
    Task<ProductImageStorageResult> SaveAsync(
        IReadOnlyList<IFormFile> files,
        string folderName,
        string productName,
        CancellationToken cancellationToken = default);

    Task<ProductImageStorageResult> SaveAdditionalAsync(
        IReadOnlyList<IFormFile> files,
        string folderName,
        string productName,
        int existingImageCount,
        CancellationToken cancellationToken = default);

    Task DeleteFilesAsync(
        IReadOnlyList<CreateProductImageRequest> images,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string folderName,
        CancellationToken cancellationToken = default);
}
```

## `src/SecureShop.Mvc/Services/Storage/ProductImageStorage.cs`

Extension: `.cs`

```csharp
using Microsoft.AspNetCore.Http;
using SecureShop.Mvc.Models.Requests;
using SecureShop.Mvc.Services.Interfaces;

namespace SecureShop.Mvc.Services.Storage;

public sealed class ProductImageStorage : IProductImageStorage
{
    private const int MaximumImageCount = 10;
    private const long MaximumImageSize = 5 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, string> AllowedExtensions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".webp"] = "image/webp"
        };

    private readonly IWebHostEnvironment _environment;

    public ProductImageStorage(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public Task<ProductImageStorageResult> SaveAsync(
        IReadOnlyList<IFormFile> files,
        string folderName,
        string productName,
        CancellationToken cancellationToken = default) =>
        SaveCoreAsync(
            files,
            folderName,
            productName,
            existingImageCount: 0,
            requireNewDirectory: true,
            cancellationToken);

    public Task<ProductImageStorageResult> SaveAdditionalAsync(
        IReadOnlyList<IFormFile> files,
        string folderName,
        string productName,
        int existingImageCount,
        CancellationToken cancellationToken = default) =>
        SaveCoreAsync(
            files,
            folderName,
            productName,
            existingImageCount,
            requireNewDirectory: false,
            cancellationToken);

    private async Task<ProductImageStorageResult> SaveCoreAsync(
        IReadOnlyList<IFormFile> files,
        string folderName,
        string productName,
        int existingImageCount,
        bool requireNewDirectory,
        CancellationToken cancellationToken)
    {
        if (files.Count is < 1 or > MaximumImageCount)
        {
            return ProductImageStorageResult.Failure(
                $"1 ile {MaximumImageCount} arasında fotoğraf seçin.");
        }

        if (existingImageCount is < 0 or > MaximumImageCount
            || existingImageCount + files.Count > MaximumImageCount)
        {
            return ProductImageStorageResult.Failure(
                $"Bir üründe en fazla {MaximumImageCount} fotoğraf olabilir.");
        }

        var normalizedFolderName = folderName.Trim();

        if (string.IsNullOrWhiteSpace(normalizedFolderName)
            || normalizedFolderName.Any(character =>
                !char.IsLetterOrDigit(character)
                && character is not '-' and not '_' and not '.'))
        {
            return ProductImageStorageResult.Failure(
                "Ürün görsel klasörü için SKU geçersiz.");
        }

        var productsRoot = Path.GetFullPath(Path.Combine(
            _environment.WebRootPath,
            "images",
            "products"));
        var productDirectory = Path.GetFullPath(Path.Combine(
            productsRoot,
            normalizedFolderName));

        EnsureInsideRoot(productsRoot, productDirectory);

        var directoryExists = Directory.Exists(productDirectory);

        if (requireNewDirectory && directoryExists)
        {
            return ProductImageStorageResult.Failure(
                "Bu SKU için görsel klasörü zaten bulunuyor.");
        }

        if (!directoryExists)
        {
            Directory.CreateDirectory(productDirectory);
        }

        var createdPaths = new List<string>(files.Count);
        try
        {
            var images = new List<CreateProductImageRequest>(files.Count);

            for (var index = 0; index < files.Count; index++)
            {
                var file = files[index];
                var extension = Path.GetExtension(file.FileName)
                    .ToLowerInvariant();

                if (!AllowedExtensions.TryGetValue(
                    extension,
                    out var expectedContentType)
                    || !string.Equals(
                        file.ContentType,
                        expectedContentType,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Yalnızca PNG, JPEG veya WebP fotoğrafları yüklenebilir.");
                }

                if (file.Length is <= 0 or > MaximumImageSize)
                {
                    throw new InvalidOperationException(
                        "Her fotoğraf en fazla 5 MB olabilir.");
                }

                if (!await HasValidSignatureAsync(
                    file,
                    extension,
                    cancellationToken))
                {
                    throw new InvalidOperationException(
                        "Fotoğraf dosyasının içeriği uzantısıyla eşleşmiyor.");
                }

                var imageNumber = existingImageCount + index + 1;
                var fileName = $"{imageNumber:D2}-{Guid.NewGuid():N}{extension}";
                var destinationPath = Path.Combine(
                    productDirectory,
                    fileName);

                createdPaths.Add(destinationPath);

                await using var destination = new FileStream(
                    destinationPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    useAsync: true);

                await file.CopyToAsync(destination, cancellationToken);

                images.Add(new CreateProductImageRequest(
                    $"/images/products/{normalizedFolderName}/{fileName}",
                    $"{productName.Trim()} - görünüm {imageNumber}"));
            }

            return ProductImageStorageResult.Success(
                normalizedFolderName,
                images);
        }
        catch (Exception exception)
            when (exception is InvalidOperationException
                or IOException
                or UnauthorizedAccessException)
        {
            foreach (var createdPath in createdPaths)
            {
                if (File.Exists(createdPath))
                {
                    File.Delete(createdPath);
                }
            }

            if (!directoryExists
                && Directory.Exists(productDirectory)
                && !Directory.EnumerateFileSystemEntries(productDirectory).Any())
            {
                Directory.Delete(productDirectory);
            }

            return ProductImageStorageResult.Failure(exception.Message);
        }
    }

    public Task DeleteFilesAsync(
        IReadOnlyList<CreateProductImageRequest> images,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var productsRoot = Path.GetFullPath(Path.Combine(
            _environment.WebRootPath,
            "images",
            "products"));

        foreach (var image in images)
        {
            var relativePath = image.ImageUrl
                .TrimStart('/')
                .Replace('/', Path.DirectorySeparatorChar);
            var candidate = Path.GetFullPath(Path.Combine(
                _environment.WebRootPath,
                relativePath));

            EnsureInsideRoot(productsRoot, candidate);

            if (File.Exists(candidate))
            {
                File.Delete(candidate);
            }

            var directory = Path.GetDirectoryName(candidate);
            if (directory is not null
                && Directory.Exists(directory)
                && !Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(
        string folderName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var productsRoot = Path.GetFullPath(Path.Combine(
            _environment.WebRootPath,
            "images",
            "products"));
        var productDirectory = Path.GetFullPath(Path.Combine(
            productsRoot,
            folderName));

        EnsureInsideRoot(productsRoot, productDirectory);

        if (Directory.Exists(productDirectory))
        {
            Directory.Delete(productDirectory, recursive: true);
        }

        return Task.CompletedTask;
    }

    private static async Task<bool> HasValidSignatureAsync(
        IFormFile file,
        string extension,
        CancellationToken cancellationToken)
    {
        var header = new byte[12];
        await using var stream = file.OpenReadStream();
        var bytesRead = await stream.ReadAsync(
            header.AsMemory(0, header.Length),
            cancellationToken);

        return extension switch
        {
            ".png" => bytesRead >= 8
                && header.AsSpan(0, 8).SequenceEqual(
                    new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            ".jpg" or ".jpeg" => bytesRead >= 3
                && header[0] == 0xFF
                && header[1] == 0xD8
                && header[2] == 0xFF,
            ".webp" => bytesRead >= 12
                && header.AsSpan(0, 4).SequenceEqual("RIFF"u8)
                && header.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };
    }

    private static void EnsureInsideRoot(
        string root,
        string candidate)
    {
        var rootWithSeparator = root.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(
            rootWithSeparator,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Ürün görsel klasörü geçersiz.");
        }
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

    Task<ApiResponse<ProductResponse>> GetManagementProductBySkuAsync(
        string sku,
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

    public Task<ApiResponse<ProductResponse>> GetManagementProductBySkuAsync(
        string sku,
        CancellationToken cancellationToken = default) =>
        GetProductResponseAsync(
            new HttpRequestMessage(
                HttpMethod.Get,
                $"api/products/management/by-sku/{Uri.EscapeDataString(sku.Trim())}"),
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
            model.Sku.Trim().ToUpperInvariant(),
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

    [HttpGet("{sku}/edit")]
    public async Task<IActionResult> Edit(
        string sku,
        CancellationToken cancellationToken)
    {
        var result = await _productApiService
            .GetManagementProductBySkuAsync(sku, cancellationToken);

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

    [HttpGet("{id:guid}/edit")]
    public async Task<IActionResult> EditById(
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

        return RedirectToAction(
            nameof(Edit),
            new
            {
                sku = result.Data.Sku
            });
    }

    [HttpPost("{sku}/edit")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(55 * 1024 * 1024)]
    public async Task<IActionResult> Edit(
        string sku,
        EditProductViewModel model,
        CancellationToken cancellationToken)
    {
        var currentResult = await _productApiService
            .GetManagementProductBySkuAsync(sku, cancellationToken);

        if (!currentResult.IsSuccess || currentResult.Data is null)
        {
            TempData["ErrorMessage"] =
                currentResult.ErrorMessage ?? "Ürün bulunamadı.";

            return RedirectToAction(nameof(Index));
        }

        var currentProduct = currentResult.Data;

        if (currentProduct.Id != model.Id)
        {
            return BadRequest();
        }

        model.Images = currentProduct.Images;

        if (currentProduct.Images.Count + model.NewImages.Count
            > MaximumImageCount)
        {
            ModelState.AddModelError(
                nameof(model.NewImages),
                $"Bir üründe en fazla {MaximumImageCount} fotoğraf olabilir.");
        }

        if (!ModelState.IsValid)
        {
            await LoadCategoriesAsync(model, cancellationToken);
            return View(model);
        }

        IReadOnlyList<CreateProductImageRequest> newImages = [];

        if (model.NewImages.Count > 0)
        {
            var storageResult = await _imageStorage.SaveAdditionalAsync(
                model.NewImages,
                model.Sku.Trim().ToUpperInvariant(),
                model.Name,
                currentProduct.Images.Count,
                cancellationToken);

            if (!storageResult.Succeeded)
            {
                ModelState.AddModelError(
                    nameof(model.NewImages),
                    storageResult.ErrorMessage
                        ?? "Yeni ürün fotoğrafları kaydedilemedi.");

                await LoadCategoriesAsync(model, cancellationToken);
                return View(model);
            }

            newImages = storageResult.Images;
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
            model.RowVersion,
            newImages);

        var result = await _productApiService.UpdateProductAsync(
            model.Id,
            request,
            cancellationToken);

        if (!result.IsSuccess || result.Data is null)
        {
            if (newImages.Count > 0)
            {
                await _imageStorage.DeleteFilesAsync(
                    newImages,
                    cancellationToken);
            }

            ModelState.AddModelError(
                string.Empty,
                result.ErrorMessage ?? "Ürün güncellenemedi.");

            await LoadCategoriesAsync(model, cancellationToken);
            return View(model);
        }

        TempData["SuccessMessage"] = newImages.Count > 0
            ? "Ürün ve yeni fotoğrafları başarıyla güncellendi."
            : "Ürün başarıyla güncellendi.";

        return RedirectToAction(
            nameof(Edit),
            new
            {
                sku = result.Data.Sku
            });
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

## `src/SecureShop.Mvc/Views/AdminProducts/Edit.cshtml`

Extension: `.cshtml`

```cshtml
@model SecureShop.Mvc.Models.ViewModels.EditProductViewModel
@{
    ViewData["Title"] = "Ürünü düzenle";
}

<div class="row justify-content-center">
    <div class="col-xl-9">
        <div class="card border-0 shadow-sm admin-product-form-card">
            <div class="card-body p-4 p-lg-5">
                <div class="d-flex justify-content-between align-items-start gap-3 mb-4">
                    <div>
                        <span class="text-uppercase text-primary fw-semibold small">Admin ürün yönetimi</span>
                        <h1 class="mt-2 mb-0">Ürünü düzenle</h1>
                    </div>
                    <a asp-action="Index" class="btn btn-outline-secondary">Listeye dön</a>
                </div>

                @if (TempData["SuccessMessage"] is string successMessage)
                {
                    <div class="alert alert-success" role="status">@successMessage</div>
                }

                @if (TempData["ErrorMessage"] is string operationError)
                {
                    <div class="alert alert-danger" role="alert">@operationError</div>
                }

                <form asp-action="Edit"
                      asp-route-sku="@Model.Sku"
                      method="post"
                      enctype="multipart/form-data">
                    <input asp-for="Id" type="hidden" />
                    <input asp-for="RowVersion" type="hidden" />
                    <div asp-validation-summary="ModelOnly" class="alert alert-danger"></div>

                    <div class="row g-3">
                        <div class="col-md-6">
                            <label asp-for="Name" class="form-label"></label>
                            <input asp-for="Name" class="form-control" />
                            <span asp-validation-for="Name" class="text-danger"></span>
                        </div>
                        <div class="col-md-6">
                            <label asp-for="Sku" class="form-label"></label>
                            <input asp-for="Sku" class="form-control" />
                            <span asp-validation-for="Sku" class="text-danger"></span>
                        </div>
                        <div class="col-md-6">
                            <label asp-for="CategoryId" class="form-label"></label>
                            <select asp-for="CategoryId" class="form-select">
                                @foreach (var category in Model.Categories)
                                {
                                    <option value="@category.Id">@category.Name</option>
                                }
                            </select>
                            <span asp-validation-for="CategoryId" class="text-danger"></span>
                        </div>
                        <div class="col-md-3">
                            <label asp-for="Price" class="form-label"></label>
                            <div class="input-group">
                                <input asp-for="Price" class="form-control" />
                                <span class="input-group-text">€</span>
                            </div>
                            <span asp-validation-for="Price" class="text-danger"></span>
                        </div>
                        <div class="col-md-3">
                            <label asp-for="StockQuantity" class="form-label"></label>
                            <input asp-for="StockQuantity" class="form-control" />
                            <span asp-validation-for="StockQuantity" class="text-danger"></span>
                        </div>
                        <div class="col-12">
                            <label asp-for="Description" class="form-label"></label>
                            <textarea asp-for="Description" class="form-control" rows="4"></textarea>
                            <span asp-validation-for="Description" class="text-danger"></span>
                        </div>
                    </div>

                    @if (Model.Images.Count > 0)
                    {
                        <div class="mt-4">
                            <h2 class="h6">Mevcut fotoğraflar</h2>
                            <div class="admin-existing-images">
                                @foreach (var image in Model.Images.OrderBy(item => item.SortOrder))
                                {
                                    <a href="@image.ImageUrl"
                                       target="_blank"
                                       rel="noopener noreferrer"
                                       title="Görseli yeni sekmede aç">
                                        <img src="@image.ImageUrl" alt="@image.AltText" />
                                    </a>
                                }
                            </div>
                        </div>
                    }

                    @if (Model.Images.Count < 10)
                    {
                        <div class="mt-4">
                            <label asp-for="NewImages" class="form-label fw-semibold"></label>
                            <div class="product-upload-zone">
                                <input asp-for="NewImages"
                                       type="file"
                                       class="form-control"
                                       accept="image/png,image/jpeg,image/webp"
                                       multiple
                                       data-product-image-input />
                                <div class="form-text mt-2">
                                    En fazla @(10 - Model.Images.Count) yeni fotoğraf ekleyebilirsiniz.
                                    PNG, JPEG veya WebP; fotoğraf başına en fazla 5 MB.
                                </div>
                                <div class="product-upload-preview mt-3"
                                     data-product-image-preview></div>
                            </div>
                            <span asp-validation-for="NewImages" class="text-danger"></span>
                        </div>
                    }
                    else
                    {
                        <div class="alert alert-secondary mt-4 mb-0">
                            Bu ürün 10 fotoğraflık üst sınıra ulaştı.
                        </div>
                    }

                    <button type="submit" class="btn btn-primary px-4 mt-4">
                        Değişiklikleri ve fotoğrafları kaydet
                    </button>
                </form>
            </div>
        </div>
    </div>
</div>

@section Scripts {
    <partial name="_ValidationScriptsPartial" />
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
                                       asp-route-sku="@product.Sku"
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

## `src/SecureShop.Mvc/wwwroot/css/site.css`

Extension: `.css`

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
.nav-cart-link { display: inline-flex; align-items: center; gap: .35rem; }
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

.cart-quantity-input { width: 5.5rem; }
.cart-item-card, .cart-summary-card, .cart-empty-state { border-radius: 1rem; }
.cart-summary-card { position: sticky; top: 1.5rem; }
.cart-empty-icon { font-size: 3rem; }

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

@media (max-width: 767.98px) {
  .product-gallery { grid-template-columns: 1fr; }
  .product-gallery-thumbnails { order: 2; flex-direction: row; overflow-x: auto; padding-bottom: .4rem; }
  .product-gallery-stage { min-height: 22rem; }
  .product-gallery-main-image { height: 22rem; }
}

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

# 7. Kod Açıklaması

## SKU tabanlı admin edit

`GET /api/products/management/by-sku/{sku}` pasif ürünler dahil yönetim
detayını SKU ile döndürür. Endpoint sınıf seviyesindeki `StaffOnly`
korumasını taşır; MVC edit controller'ı ayrıca yalnızca `Admin` rolüne açıktır.

MVC rotası:

```text
GET  /admin/products/{sku}/edit
POST /admin/products/{sku}/edit
```

Eski `{id:guid}/edit` GET rotası korunur ve güvenilir API response'undaki SKU
ile yeni adrese yönlendirir.

## Fotoğraf ekleme

`ProductImageStorage.SaveAdditionalAsync`:

- PNG, JPEG ve WebP kabul eder.
- Content-Type ve dosya imzasını birlikte doğrular.
- Dosya başına 5 MB sınırı uygular.
- SKU klasöründen dışarı çıkılmasını engeller.
- Çakışmayan GUID tabanlı dosya adı üretir.
- Başarısızlıkta yalnızca o istekte oluşturulan dosyaları siler.

MVC dosyayı kaydettikten sonra URL ve alt metni API
`UpdateProductRequest.Images` alanına ekler. API ürünün mevcut görsel sayısını
veritabanından okur; MVC'nin bildirdiği sayıya güvenmez. Toplam sayı 10'u
aşarsa işlem reddedilir.

Ürün alanları ve yeni `ProductImage` entity'leri aynı EF Core
`SaveChangesAsync` çağrısında kaydedilir. RowVersion eşzamanlı güncelleme
kontrolü devam eder.

# 8. API–MVC Veri Akışı

```text
Admin Edit Razor formu
    ↓ multipart/form-data
AdminProductsController
    ↓
IProductImageStorage.SaveAdditionalAsync
    ↓ güvenli SKU klasörü
wwwroot/images/products/{SKU}/
    ↓ görsel URL metadata'sı
IProductApiService.UpdateProductAsync
    ↓ HttpClient PUT
SecureShop.Api /api/products/{id}
    ↓
ProductService.UpdateAsync
    ↓ RowVersion + toplam 10 görsel kontrolü
Product.AddImage
    ↓
AppDbContext.SaveChangesAsync
    ↓
SQL Server ProductImages
```

# 9. Uygulama Sırası

1. API update request ve mutation durumunu genişlet.
2. API SKU yönetim sorgusunu ekle.
3. API update servisinde yeni görselleri transaction'a dahil et.
4. MVC request ve ViewModel modellerini genişlet.
5. Dosya storage servisine ekleme ve seçili dosya rollback işlemlerini ekle.
6. MVC typed API service'e SKU yönetim çağrısını ekle.
7. Admin controller rotalarını ve upload akışını güncelle.
8. Edit ve liste Razor view'larını güncelle.
9. CSS, build ve uçtan uca testleri tamamla.

# 10. Çalıştırma ve Test

Admin hesabıyla giriş yaptıktan sonra:

```text
https://localhost:7002/admin/products/SSH-CAMERA-01/edit
```

adresini açın. `Yeni fotoğraflar` alanından bir veya daha fazla görsel seçip
`Değişiklikleri ve fotoğrafları kaydet` düğmesine basın.

Gerçekleştirilen otomatik entegrasyon kontrolü:

```text
SKU edit GET: 200
Upload alanı: mevcut
Eski GUID edit GET: 302
Eski GUID Location: /admin/products/{SKU}/edit
Edit + ikinci görsel POST: 302
Yönlendirme: /admin/products/{SKU}/edit
API görsel sayısı: 2
Fiyat: 59.90
Stok: 9
İki statik görsel URL'si: 200, 200
```

Geçici test ürünü, ProductImage kayıtları ve otomatik SKU klasörü test
sonunda temizlendi.

# 11. Beklenen Sonuç

- Admin listesinde `Düzenle` bağlantısı SKU adresi üretir.
- Edit sayfası ürün bilgilerini ve mevcut görselleri gösterir.
- Mevcut görsele tıklanınca görsel yeni sekmede açılır.
- Yeni görseller seçildiğinde istemci ön izlemesi gösterilir.
- Kaydetme sonrası yeni görseller detay galerisinde görünür.
- Ürün 10 görsele ulaştığında yeni dosya alanı yerine sınır mesajı görünür.

# 12. Yaygın Hatalar

## SKU edit adresi `404`

Route metadata'sı çalışan Debug sürecine yüklenmemiş olabilir. API ve MVC
watcher terminallerinde `Ctrl+R` kullanın.

## `MSB3027` veya `MSB3021`

Aynı proje için birden fazla `dotnet run/watch` çalışıyordur. Her proje için
yalnızca bir watcher bırakın.

## “Bir üründe en fazla 10 fotoğraf olabilir”

Mevcut ve yeni fotoğrafların toplamı 10'u geçmiştir. Bu sınır API tarafından
yeniden doğrulanır.

## Dosya uzantısı doğru olduğu halde reddediliyor

Dosyanın gerçek imzası uzantısı veya Content-Type ile uyuşmuyordur. Dosyayı
geçerli PNG, JPEG veya WebP olarak yeniden dışa aktarın.

## API güncellemesi başarısız

MVC o istekte yazdığı yeni dosyaları otomatik geri alır. Önceden var olan ürün
klasörü ve görseller silinmez.

# 13. Tamamlama Kontrol Listesi

```text
[x] CLI komutları doğru klasörde çalıştırıldı
[x] Dosyalar doğru projede oluşturuldu
[x] Web API hatasız derlendi
[x] MVC uygulaması hatasız derlendi
[x] MVC, Web API'ye başarıyla bağlandı
[x] MVC doğrudan veritabanına erişmiyor
[x] Web API ProductImages kaydını gerçekleştirdi
[x] Admin edit URL'si SKU tabanlı oldu
[x] Eski GUID edit URL'si SKU URL'sine yönlendirildi
[x] Mevcut ürüne çoklu fotoğraf yükleme alanı eklendi
[x] Dosya türü, imza, boyut ve path traversal kontrolleri uygulandı
[x] Toplam 10 fotoğraf sınırı API'de doğrulandı
[x] Başarısız API işleminde yeni dosya rollback'i uygulandı
[x] Uçtan uca upload akışı doğrulandı
[x] Test verileri ve geçici süreçler temizlendi
```
