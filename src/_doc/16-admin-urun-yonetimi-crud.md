# 16 — Admin ürün yönetimi ve CRUD

## Amaç

Admin kullanıcısı ürünleri ayrı bir yönetim ekranında görebilir, yeni ürün ve birden fazla fotoğraf ekleyebilir, ürün bilgilerini düzenleyebilir, ürünü pasife alabilir ve daha sonra yeniden aktifleştirebilir.

Fiziksel `DELETE` yerine soft delete kullanıldı. Sipariş, sepet ve audit ilişkilerinin geçmişi korunurken pasif ürünler müşteriye açık katalogdan çıkar.

## Yetkilendirme

- MVC yönetim controller'ı yalnızca `Admin` rolüne açıktır.
- API oluşturma, güncelleme ve durum değiştirme endpoint'leri `AdminOnly` policy ile korunur.
- Anti-forgery doğrulaması tüm MVC POST formlarında zorunludur.
- Eşzamanlı değişiklikleri yakalamak için `RowVersion` her güncelleme ve durum değişikliğinde gönderilir.

## Yönetim rotaları

| İşlem | MVC rota | HTTP |
|---|---|---|
| Liste | `/admin/products` | GET |
| Yeni ürün formu | `/admin/products/create` | GET |
| Yeni ürün kaydı | `/admin/products/create` | POST |
| Düzenleme formu | `/admin/products/{id}/edit` | GET |
| Ürün güncelleme | `/admin/products/{id}/edit` | POST |
| Pasife alma / aktifleştirme | `/admin/products/{id}/status` | POST |

## Ekran davranışı

- Navbar'daki Admin menüsünde `Ürün yönetimi` ve `Yeni ürün ekle` bağlantıları bulunur.
- Yönetim listesinde görsel, ad, SKU, kategori, fiyat, stok ve aktiflik durumu gösterilir.
- `+ Yeni ürün` butonu çoklu fotoğraf yükleme formunu açar.
- `Düzenle` ile kategori, ad, SKU, açıklama, fiyat ve stok değiştirilebilir.
- `Pasife al` katalogdan güvenli kaldırma; `Aktifleştir` geri alma işlemidir.

## Fotoğraf klasörü

Yeni ürün kaydında klasör SKU'dan otomatik oluşturulur:

```text
src/SecureShop.Mvc/wwwroot/images/products/{sku-kucuk-harf}/
├── 01-{guid}.png
├── 02-{guid}.jpg
└── ...
```

Fotoğraf yükleme güvenliği ve galeri yapısı `15-urun-gorsel-galerisi.md` belgesinde ayrıntılıdır.

## Doğrulama sonucu

- Admin login ve `/admin/products` liste ekranı: başarılı (`200`).
- Bir fotoğraflı geçici ürün oluşturma: başarılı (`302` yönlendirme).
- Ürünün yönetim listesinde görünmesi: başarılı.
- Düzenleme formu ve `RowVersion`: başarılı (`200`).
- Ad, fiyat ve stok güncelleme: başarılı (`302`).
- Pasife alma: başarılı (`302`).
- SQL doğrulaması: `IsActive=0`, `Price=59.90`, `StockQuantity=9`, `ImageCount=1`.
- Doğrulama ürünü ve otomatik görsel klasörü test sonunda temizlendi.
- `dotnet build SecureShop.sln -c Release --no-restore`: başarılı, `0` uyarı ve `0` hata.
- `dotnet test SecureShop.sln -c Release --no-build`: başarılı.
- EF Core pending model kontrolü: migration gerektiren değişiklik yok.
- `git diff --check`: whitespace hatası yok.

## Dosyalar ve tam kaynak kodları

### `src/SecureShop.Api/Contracts/Requests/UpdateProductRequest.cs`

Uzantı: `.cs`

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
}
```

### `src/SecureShop.Api/Contracts/Requests/SetProductStatusRequest.cs`

Uzantı: `.cs`

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

### `src/SecureShop.Api/Controllers/ProductsController.cs`

Uzantı: `.cs`

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

### `src/SecureShop.Api/Features/Products/IProductService.cs`

Uzantı: `.cs`

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

### `src/SecureShop.Api/Features/Products/ProductMutationResult.cs`

Uzantı: `.cs`

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

### `src/SecureShop.Api/Features/Products/ProductService.cs`

Uzantı: `.cs`

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

### `src/SecureShop.Mvc/Models/Requests/UpdateProductRequest.cs`

Uzantı: `.cs`

```csharp
namespace SecureShop.Mvc.Models.Requests;

public sealed record UpdateProductRequest(
    Guid CategoryId,
    string Name,
    string Sku,
    string? Description,
    decimal Price,
    int StockQuantity,
    string RowVersion);
```

### `src/SecureShop.Mvc/Models/Requests/SetProductStatusRequest.cs`

Uzantı: `.cs`

```csharp
namespace SecureShop.Mvc.Models.Requests;

public sealed record SetProductStatusRequest(
    bool IsActive,
    string RowVersion);
```

### `src/SecureShop.Mvc/Models/ViewModels/AdminProductListViewModel.cs`

Uzantı: `.cs`

```csharp
using SecureShop.Mvc.Models.Responses;

namespace SecureShop.Mvc.Models.ViewModels;

public sealed class AdminProductListViewModel
{
    public IReadOnlyList<ProductResponse> Products { get; init; } = [];

    public string? ErrorMessage { get; init; }
}
```

### `src/SecureShop.Mvc/Models/ViewModels/EditProductViewModel.cs`

Uzantı: `.cs`

```csharp
using System.ComponentModel.DataAnnotations;
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

    public IReadOnlyList<CategoryOptionResponse> Categories { get; set; } = [];
}
```

### `src/SecureShop.Mvc/Controllers/AdminProductsController.cs`

Uzantı: `.cs`

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
                id = result.Data.Id
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

### `src/SecureShop.Mvc/Services/Interfaces/IProductApiService.cs`

Uzantı: `.cs`

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

### `src/SecureShop.Mvc/Services/Api/ProductApiService.cs`

Uzantı: `.cs`

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

### `src/SecureShop.Mvc/Views/AdminProducts/Index.cshtml`

Uzantı: `.cshtml`

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
                                       asp-route-id="@product.Id"
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

### `src/SecureShop.Mvc/Views/AdminProducts/Create.cshtml`

Uzantı: `.cshtml`

```cshtml
@model SecureShop.Mvc.Models.ViewModels.CreateProductViewModel
@{
    ViewData["Title"] = "Yeni ürün ekle";
}

<div class="row justify-content-center">
    <div class="col-xl-9">
        <div class="card border-0 shadow-sm admin-product-form-card">
            <div class="card-body p-4 p-lg-5">
                <div class="mb-4">
                    <span class="text-uppercase text-primary fw-semibold small">Admin ürün yönetimi</span>
                    <h1 class="mt-2">Yeni ürün ekle</h1>
                    <p class="text-body-secondary mb-0">
                        Birden fazla fotoğraf seçebilirsiniz. İlk fotoğraf ana ürün görseli olur.
                    </p>
                </div>

                <form asp-action="Create"
                      method="post"
                      enctype="multipart/form-data">
                    <div asp-validation-summary="ModelOnly" class="alert alert-danger"></div>

                    <div class="row g-3">
                        <div class="col-md-6">
                            <label asp-for="Name" class="form-label"></label>
                            <input asp-for="Name" class="form-control" />
                            <span asp-validation-for="Name" class="text-danger"></span>
                        </div>
                        <div class="col-md-6">
                            <label asp-for="Sku" class="form-label"></label>
                            <input asp-for="Sku" class="form-control" autocomplete="off" />
                            <span asp-validation-for="Sku" class="text-danger"></span>
                            <div class="form-text">SKU aynı zamanda otomatik görsel klasörü olur.</div>
                        </div>
                        <div class="col-md-6">
                            <label asp-for="CategoryId" class="form-label"></label>
                            <select asp-for="CategoryId" class="form-select">
                                <option value="">Kategori seçin</option>
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
                        <div class="col-12">
                            <label asp-for="Images" class="form-label"></label>
                            <div class="product-upload-zone">
                                <input asp-for="Images"
                                       type="file"
                                       class="form-control"
                                       accept="image/png,image/jpeg,image/webp"
                                       multiple
                                       data-product-image-input />
                                <div class="form-text mt-2">
                                    PNG, JPEG veya WebP; en fazla 10 fotoğraf ve fotoğraf başına 5 MB.
                                </div>
                                <div class="product-upload-preview mt-3"
                                     data-product-image-preview></div>
                            </div>
                            <span asp-validation-for="Images" class="text-danger"></span>
                        </div>
                    </div>

                    <div class="d-flex gap-2 mt-4">
                        <button type="submit" class="btn btn-primary px-4">
                            Ürünü ve fotoğrafları kaydet
                        </button>
                        <a asp-controller="Products" asp-action="Index" class="btn btn-outline-secondary">
                            İptal
                        </a>
                    </div>
                </form>
            </div>
        </div>
    </div>
</div>

@section Scripts {
    <partial name="_ValidationScriptsPartial" />
}
```

### `src/SecureShop.Mvc/Views/AdminProducts/Edit.cshtml`

Uzantı: `.cshtml`

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

                <form asp-action="Edit" asp-route-id="@Model.Id" method="post">
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
                                    <img src="@image.ImageUrl" alt="@image.AltText" />
                                }
                            </div>
                        </div>
                    }

                    <button type="submit" class="btn btn-primary px-4 mt-4">
                        Değişiklikleri kaydet
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

### `src/SecureShop.Mvc/Views/Shared/_AuthenticationMenu.cshtml`

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
                @if (User.IsInRole(AppRoles.Admin))
                {
                    <li>
                        <a class="dropdown-item" asp-controller="AdminProducts" asp-action="Index">
                            Ürün yönetimi
                        </a>
                    </li>
                    <li>
                        <a class="dropdown-item" asp-controller="AdminProducts" asp-action="Create">
                            Yeni ürün ekle
                        </a>
                    </li>
                }
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

### `src/SecureShop.Mvc/wwwroot/css/site.css`

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

