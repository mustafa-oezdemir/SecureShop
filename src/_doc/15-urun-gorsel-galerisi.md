# Ürün Görsel Galerisi

# 1. Bu Adımın Amacı

Ürünlere sıralı görseller eklemek ve detay sayfasında Amazon benzeri thumbnail–aktif fotoğraf galerisi göstermek.

# 2. Etkilenen Uygulama

```text
SecureShop.Api
SecureShop.Mvc
SQL Server
```

# 3. Yapılanlar

- `ProductImage` entity ve SQL tablosu eklendi.
- Bir üründe görsel sırası benzersiz, primary görsel tek olacak şekilde indeksler eklendi.
- API ürün response'una sıralı görseller eklendi.
- Development kataloğuna 5 ürün eklendi.
- Kulaklık ürünü için 5 gerçek stüdyo görseli üretildi ve projeye kaydedildi.
- Diğer ürün görselleri kullanıcı tarafından belirleneceği için şimdilik görselsiz bırakıldı; kırık URL üretilmiyor.
- Ürün listesinde primary görsel, detayda 5 thumbnail ve aktif ana görsel eklendi.
- Thumbnail tıklaması JavaScript ile ana görseli ve aktif kenarlığı değiştiriyor.
- Mobil yatay thumbnail düzeni eklendi.
- Admin ürün oluşturma formuna çoklu fotoğraf yükleme eklendi.
- Her yeni ürün için SKU tabanlı `images/products/{sku}/` klasörü otomatik oluşturulur.
- PNG, JPEG ve WebP imzası doğrulanır; en fazla 10 dosya ve dosya başına 5 MB kabul edilir.
- API başarısız olursa yalnızca o işlemde oluşturulan yeni görsel klasörü temizlenir.

# 4. CLI Komutları

Çalışma dizini: `SecureShop/`

```powershell
dotnet ef migrations add AddProductImages --project src/SecureShop.Api/SecureShop.Api.csproj --startup-project src/SecureShop.Api/SecureShop.Api.csproj --configuration Release --output-dir Data/Migrations
dotnet build SecureShop.sln --configuration Release
dotnet ef database update --project src/SecureShop.Api/SecureShop.Api.csproj --startup-project src/SecureShop.Api/SecureShop.Api.csproj --configuration Release --no-build
```

# 5. Güncel Proje Yapısı

```text
src/SecureShop.Api/
├── Domain/Entities/ProductImage.cs
├── Data/Configurations/ProductImageConfiguration.cs
├── Data/Seed/CatalogSeeder.cs
├── Contracts/Responses/ProductImageResponse.cs
└── Data/Migrations/20260716223305_AddProductImages.cs

src/SecureShop.Mvc/
├── Controllers/AdminProductsController.cs
├── Services/Storage/ProductImageStorage.cs
├── Views/AdminProducts/Create.cshtml
├── Models/Responses/ProductImageResponse.cs
├── Views/Products/Index.cshtml
├── Views/Products/Details.cshtml
└── wwwroot/
    ├── js/site.js
    ├── css/site.css
    └── images/products/
        └── headphones/
            ├── 1.png
            ├── 2.png
            ├── 3.png
            ├── 4.png
            └── 5.png
```

# 6. Dosya Bazında Eksiksiz Kodlar

## `src/SecureShop.Api/Domain/Entities/ProductImage.cs`

Uzantı: `.cs`

```csharp
namespace SecureShop.Api.Domain.Entities;

public sealed class ProductImage
{
    private ProductImage()
    {
    }

    public ProductImage(
        Guid productId,
        string imageUrl,
        string altText,
        int sortOrder,
        bool isPrimary = false)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException(
                "Ürün kimliği boş olamaz.",
                nameof(productId));
        }

        Id = Guid.NewGuid();
        ProductId = productId;
        SetImageUrl(imageUrl);
        SetAltText(altText);
        SetSortOrder(sortOrder);
        IsPrimary = isPrimary;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid ProductId { get; private set; }

    public string ImageUrl { get; private set; } = string.Empty;

    public string AltText { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    public bool IsPrimary { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public Product Product { get; private set; } = null!;

    private void SetImageUrl(string imageUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imageUrl);

        var normalizedUrl = imageUrl.Trim();

        if (normalizedUrl.Length > 500)
        {
            throw new ArgumentOutOfRangeException(
                nameof(imageUrl),
                "Görsel adresi 500 karakterden uzun olamaz.");
        }

        ImageUrl = normalizedUrl;
    }

    private void SetAltText(string altText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(altText);

        var normalizedAltText = altText.Trim();

        if (normalizedAltText.Length > 200)
        {
            throw new ArgumentOutOfRangeException(
                nameof(altText),
                "Görsel açıklaması 200 karakterden uzun olamaz.");
        }

        AltText = normalizedAltText;
    }

    private void SetSortOrder(int sortOrder)
    {
        if (sortOrder is < 0 or > 99)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sortOrder),
                "Görsel sırası 0 ile 99 arasında olmalıdır.");
        }

        SortOrder = sortOrder;
    }
}
```

## `src/SecureShop.Api/Data/Configurations/ProductImageConfiguration.cs`

Uzantı: `.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecureShop.Api.Domain.Entities;

namespace SecureShop.Api.Data.Configurations;

public sealed class ProductImageConfiguration
    : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable(
            "ProductImages",
            tableBuilder => tableBuilder.HasCheckConstraint(
                "CK_ProductImages_SortOrder_Range",
                "[SortOrder] BETWEEN 0 AND 99"));

        builder.HasKey(image => image.Id);

        builder.Property(image => image.Id)
            .ValueGeneratedNever();

        builder.Property(image => image.ProductId)
            .IsRequired();

        builder.Property(image => image.ImageUrl)
            .HasMaxLength(500)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(image => image.AltText)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(image => image.SortOrder)
            .IsRequired();

        builder.Property(image => image.IsPrimary)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(image => image.CreatedAtUtc)
            .HasPrecision(0)
            .IsRequired();

        builder.HasIndex(image => new
        {
            image.ProductId,
            image.SortOrder
        })
            .IsUnique()
            .HasDatabaseName("UX_ProductImages_ProductId_SortOrder");

        builder.HasIndex(image => image.ProductId)
            .IsUnique()
            .HasFilter("[IsPrimary] = 1")
            .HasDatabaseName("UX_ProductImages_ProductId_Primary");

        builder.HasOne(image => image.Product)
            .WithMany(product => product.Images)
            .HasForeignKey(image => image.ProductId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
```

## `src/SecureShop.Api/Contracts/Requests/CreateProductImageRequest.cs`

Uzantı: `.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace SecureShop.Api.Contracts.Requests;

public sealed class CreateProductImageRequest
{
    [Required]
    [StringLength(500)]
    [RegularExpression(
        "^/images/products/[A-Za-z0-9._-]+/[A-Za-z0-9._-]+\\.(png|jpg|jpeg|webp)$",
        ErrorMessage = "Ürün görsel adresi geçersiz.")]
    public string ImageUrl { get; init; } = string.Empty;

    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string AltText { get; init; } = string.Empty;
}
```

## `src/SecureShop.Api/Contracts/Requests/CreateProductRequest.cs`

Uzantı: `.cs`

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

    [Range(
        typeof(decimal),
        "0",
        "9999999999999999.99",
        ParseLimitsInInvariantCulture = true)]
    public decimal Price { get; init; }

    [Range(0, int.MaxValue)]
    public int StockQuantity { get; init; }

    [MaxLength(10)]
    public IReadOnlyList<CreateProductImageRequest> Images { get; init; } = [];
}
```

## `src/SecureShop.Api/Contracts/Responses/ProductImageResponse.cs`

Uzantı: `.cs`

```csharp
namespace SecureShop.Api.Contracts.Responses;

public sealed record ProductImageResponse(
    Guid Id,
    string ImageUrl,
    string AltText,
    int SortOrder,
    bool IsPrimary);
```

## `src/SecureShop.Api/Contracts/Responses/ProductResponse.cs`

Uzantı: `.cs`

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
    IReadOnlyList<ProductImageResponse> Images,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    string RowVersion);
```

## `src/SecureShop.Api/Domain/Entities/Product.cs`

Uzantı: `.cs`

```csharp
namespace SecureShop.Api.Domain.Entities;

public sealed class Product
{
    private Product()
    {
    }

    public Product(
        Guid categoryId,
        string name,
        string sku,
        decimal price,
        int stockQuantity,
        string? description = null)
    {
        if (categoryId == Guid.Empty)
        {
            throw new ArgumentException(
                "Kategori kimliği boş olamaz.",
                nameof(categoryId));
        }

        Id = Guid.NewGuid();
        CategoryId = categoryId;

        SetName(name);
        SetSku(sku);
        SetDescription(description);
        SetPrice(price);
        SetStockQuantity(stockQuantity);

        IsActive = true;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid CategoryId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Sku { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public decimal Price { get; private set; }

    public int StockQuantity { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public Category Category { get; private set; } = null!;

    public ICollection<ProductImage> Images { get; private set; } =
        new List<ProductImage>();

    public void AddImage(
        string imageUrl,
        string altText,
        int sortOrder,
        bool isPrimary = false)
    {
        if (Images.Any(image => image.SortOrder == sortOrder))
        {
            throw new InvalidOperationException(
                "Aynı görsel sırası bir ürün içinde tekrar kullanılamaz.");
        }

        if (isPrimary && Images.Any(image => image.IsPrimary))
        {
            throw new InvalidOperationException(
                "Bir ürünün yalnızca bir ana görseli olabilir.");
        }

        Images.Add(new ProductImage(
            Id,
            imageUrl,
            altText,
            sortOrder,
            isPrimary));

        MarkAsUpdated();
    }

    public void SetName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var normalizedName = name.Trim();

        if (normalizedName.Length > 200)
        {
            throw new ArgumentOutOfRangeException(
                nameof(name),
                "Ürün adı 200 karakterden uzun olamaz.");
        }

        Name = normalizedName;
        MarkAsUpdated();
    }

    public void SetSku(string sku)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);

        var normalizedSku = sku.Trim().ToUpperInvariant();

        if (normalizedSku.Length > 64)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sku),
                "SKU 64 karakterden uzun olamaz.");
        }

        Sku = normalizedSku;
        MarkAsUpdated();
    }

    public void SetDescription(string? description)
    {
        var normalizedDescription = string.IsNullOrWhiteSpace(description)
            ? null
            : description.Trim();

        if (normalizedDescription?.Length > 2000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(description),
                "Ürün açıklaması 2000 karakterden uzun olamaz.");
        }

        Description = normalizedDescription;
        MarkAsUpdated();
    }

    public void SetPrice(decimal price)
    {
        if (price < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(price),
                "Ürün fiyatı negatif olamaz.");
        }

        Price = decimal.Round(
            price,
            decimals: 2,
            mode: MidpointRounding.ToEven);

        MarkAsUpdated();
    }

    public void SetStockQuantity(int stockQuantity)
    {
        if (stockQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stockQuantity),
                "Stok miktarı negatif olamaz.");
        }

        StockQuantity = stockQuantity;
        MarkAsUpdated();
    }

    public void ChangeCategory(Guid categoryId)
    {
        if (categoryId == Guid.Empty)
        {
            throw new ArgumentException(
                "Kategori kimliği boş olamaz.",
                nameof(categoryId));
        }

        CategoryId = categoryId;
        MarkAsUpdated();
    }

    public void Activate()
    {
        IsActive = true;
        MarkAsUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        MarkAsUpdated();
    }

    private void MarkAsUpdated()
    {
        if (CreatedAtUtc != default)
        {
            UpdatedAtUtc = DateTimeOffset.UtcNow;
        }
    }
}
```

## `src/SecureShop.Api/Data/Seed/CatalogSeeder.cs`

Uzantı: `.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using SecureShop.Api.Domain.Entities;

namespace SecureShop.Api.Data.Seed;

public sealed class CatalogSeeder
{
    private static readonly IReadOnlyList<ProductSeedDefinition> Products =
    [
        new(
            "Kablosuz Gürültü Engelleyici Kulaklık",
            "SSH-HEADPHONE-01",
            "Aktif gürültü engelleme, şeffaf mod ve 40 saate kadar pil ömrü sunan premium kablosuz kulaklık.",
            249.90m,
            24,
            "headphones",
            5),
        new(
            "Akıllı Spor Saati",
            "SSH-WATCH-01",
            "AMOLED ekran, GPS, sağlık takibi ve suya dayanıklı alüminyum gövdeli akıllı saat.",
            189.90m,
            31,
            "smartwatch",
            0),
        new(
            "RGB Mekanik Klavye",
            "SSH-KEYBOARD-01",
            "Hot-swap mekanik switch, RGB aydınlatma ve kompakt alüminyum kasaya sahip oyuncu klavyesi.",
            139.90m,
            18,
            "keyboard",
            0),
        new(
            "Taşınabilir Bluetooth Hoparlör",
            "SSH-SPEAKER-01",
            "360 derece ses, IP67 koruma ve 18 saat pil ömrü sunan taşınabilir Bluetooth hoparlör.",
            119.90m,
            42,
            "speaker",
            0),
        new(
            "4K Aksiyon Kamerası",
            "SSH-CAMERA-01",
            "4K video, gelişmiş görüntü sabitleme ve su geçirmez gövdeye sahip kompakt aksiyon kamerası.",
            329.90m,
            15,
            "action-camera",
            0)
    ];

    private readonly AppDbContext _dbContext;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<CatalogSeeder> _logger;

    public CatalogSeeder(
        AppDbContext dbContext,
        IHostEnvironment environment,
        ILogger<CatalogSeeder> logger)
    {
        _dbContext = dbContext;
        _environment = environment;
        _logger = logger;
    }

    public async Task SeedAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_environment.IsDevelopment())
        {
            return;
        }

        var category = await _dbContext.Categories
            .SingleOrDefaultAsync(
                item => item.Name == "Elektronik",
                cancellationToken);

        if (category is null)
        {
            category = new Category("Elektronik");
            _dbContext.Categories.Add(category);
        }

        foreach (var definition in Products)
        {
            var product = await _dbContext.Products
                .Include(item => item.Images)
                .SingleOrDefaultAsync(
                    item => item.Sku == definition.Sku,
                    cancellationToken);

            if (product is null)
            {
                product = new Product(
                    category.Id,
                    definition.Name,
                    definition.Sku,
                    definition.Price,
                    definition.StockQuantity,
                    definition.Description);

                _dbContext.Products.Add(product);
            }

            var expectedImageUrls = Enumerable
                .Range(1, definition.ImageCount)
                .Select(index =>
                    $"/images/products/{definition.AssetPrefix}/{index}.png")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var obsoleteImage in product.Images
                .Where(image => !expectedImageUrls.Contains(image.ImageUrl))
                .ToList())
            {
                product.Images.Remove(obsoleteImage);
                _dbContext.ProductImages.Remove(obsoleteImage);
            }

            for (var index = 1; index <= definition.ImageCount; index++)
            {
                var sortOrder = index - 1;

                if (product.Images.Any(
                    image => image.SortOrder == sortOrder))
                {
                    continue;
                }

                product.AddImage(
                    $"/images/products/{definition.AssetPrefix}/{index}.png",
                    $"{definition.Name} - görünüm {index}",
                    sortOrder,
                    isPrimary: index == 1);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Development catalog synchronized with {ProductCount} products and {ImageCount} images.",
            Products.Count,
            Products.Sum(product => product.ImageCount));
    }

    private sealed record ProductSeedDefinition(
        string Name,
        string Sku,
        string Description,
        decimal Price,
        int StockQuantity,
        string AssetPrefix,
        int ImageCount);
}
```

## `src/SecureShop.Api/Controllers/ProductsController.cs`

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

## `src/SecureShop.Api/Data/Migrations/20260716223305_AddProductImages.cs`

Uzantı: `.cs`

```csharp
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureShop.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProductImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImageUrl = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: false),
                    AltText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductImages", x => x.Id);
                    table.CheckConstraint("CK_ProductImages_SortOrder_Range", "[SortOrder] BETWEEN 0 AND 99");
                    table.ForeignKey(
                        name: "FK_ProductImages_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_ProductImages_ProductId_Primary",
                table: "ProductImages",
                column: "ProductId",
                unique: true,
                filter: "[IsPrimary] = 1");

            migrationBuilder.CreateIndex(
                name: "UX_ProductImages_ProductId_SortOrder",
                table: "ProductImages",
                columns: new[] { "ProductId", "SortOrder" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductImages");
        }
    }
}
```

## `src/SecureShop.Api/Features/Products/ProductService.cs`

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

## `src/SecureShop.Mvc/Models/Requests/CreateProductImageRequest.cs`

Uzantı: `.cs`

```csharp
namespace SecureShop.Mvc.Models.Requests;

public sealed record CreateProductImageRequest(
    string ImageUrl,
    string AltText);
```

## `src/SecureShop.Mvc/Models/Requests/CreateProductRequest.cs`

Uzantı: `.cs`

```csharp
namespace SecureShop.Mvc.Models.Requests;

public sealed record CreateProductRequest(
    Guid CategoryId,
    string Name,
    string Sku,
    string? Description,
    decimal Price,
    int StockQuantity,
    IReadOnlyList<CreateProductImageRequest> Images);
```

## `src/SecureShop.Mvc/Models/ViewModels/CreateProductViewModel.cs`

Uzantı: `.cs`

```csharp
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using SecureShop.Mvc.Models.Responses;

namespace SecureShop.Mvc.Models.ViewModels;

public sealed class CreateProductViewModel
{
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

    [Required(ErrorMessage = "En az bir ürün fotoğrafı seçin.")]
    [Display(Name = "Ürün fotoğrafları")]
    public List<IFormFile> Images { get; set; } = [];

    public IReadOnlyList<CategoryOptionResponse> Categories { get; set; } = [];
}
```

## `src/SecureShop.Mvc/Services/Interfaces/IProductImageStorage.cs`

Uzantı: `.cs`

```csharp
using Microsoft.AspNetCore.Http;
using SecureShop.Mvc.Services.Storage;

namespace SecureShop.Mvc.Services.Interfaces;

public interface IProductImageStorage
{
    Task<ProductImageStorageResult> SaveAsync(
        IReadOnlyList<IFormFile> files,
        string folderName,
        string productName,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string folderName,
        CancellationToken cancellationToken = default);
}
```

## `src/SecureShop.Mvc/Services/Storage/ProductImageStorageResult.cs`

Uzantı: `.cs`

```csharp
using SecureShop.Mvc.Models.Requests;

namespace SecureShop.Mvc.Services.Storage;

public sealed record ProductImageStorageResult(
    bool Succeeded,
    string FolderName,
    IReadOnlyList<CreateProductImageRequest> Images,
    string? ErrorMessage)
{
    public static ProductImageStorageResult Success(
        string folderName,
        IReadOnlyList<CreateProductImageRequest> images) =>
        new(true, folderName, images, null);

    public static ProductImageStorageResult Failure(
        string errorMessage) =>
        new(false, string.Empty, [], errorMessage);
}
```

## `src/SecureShop.Mvc/Services/Storage/ProductImageStorage.cs`

Uzantı: `.cs`

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

    public async Task<ProductImageStorageResult> SaveAsync(
        IReadOnlyList<IFormFile> files,
        string folderName,
        string productName,
        CancellationToken cancellationToken = default)
    {
        if (files.Count is < 1 or > MaximumImageCount)
        {
            return ProductImageStorageResult.Failure(
                $"1 ile {MaximumImageCount} arasında fotoğraf seçin.");
        }

        var normalizedFolderName = folderName.Trim().ToLowerInvariant();

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

        if (Directory.Exists(productDirectory))
        {
            return ProductImageStorageResult.Failure(
                "Bu SKU için görsel klasörü zaten bulunuyor.");
        }

        Directory.CreateDirectory(productDirectory);

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

                var fileName = $"{index + 1:D2}-{Guid.NewGuid():N}{extension}";
                var destinationPath = Path.Combine(
                    productDirectory,
                    fileName);

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
                    $"{productName.Trim()} - görünüm {index + 1}"));
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
            if (Directory.Exists(productDirectory))
            {
                Directory.Delete(productDirectory, recursive: true);
            }

            return ProductImageStorageResult.Failure(exception.Message);
        }
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

## `src/SecureShop.Mvc/Controllers/AdminProductsController.cs`

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
}
```

## `src/SecureShop.Mvc/Services/Interfaces/IProductApiService.cs`

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
}
```

## `src/SecureShop.Mvc/Services/Api/ProductApiService.cs`

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

## `src/SecureShop.Mvc/Models/Responses/ProductImageResponse.cs`

Uzantı: `.cs`

```csharp
namespace SecureShop.Mvc.Models.Responses;

public sealed record ProductImageResponse(
    Guid Id,
    string ImageUrl,
    string AltText,
    int SortOrder,
    bool IsPrimary);
```

## `src/SecureShop.Mvc/Models/Responses/ProductResponse.cs`

Uzantı: `.cs`

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
    IReadOnlyList<ProductImageResponse> Images,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    string RowVersion);
```

## `src/SecureShop.Mvc/Models/Responses/CategoryOptionResponse.cs`

Uzantı: `.cs`

```csharp
namespace SecureShop.Mvc.Models.Responses;

public sealed record CategoryOptionResponse(
    Guid Id,
    string Name);
```

## 7. SSH-CAMERA-01 görsellerinin bağlanması

`src/SecureShop.Mvc/wwwroot/images/products/SSH-CAMERA-01` klasöründe bulunan
`1.png`, `2.png`, `3.png` ve `4.png` dosyaları `SSH-CAMERA-01` SKU'lu
`4K Aksiyon Kamerası` ürününe bağlandı.

- Klasör adı: `SSH-CAMERA-01`
- Görsel sayısı: `4`
- Ana görsel: `1.png`
- Galeri sırası: `1.png` → `4.png`
- API doğrulaması: `images.Count = 4`
- MVC detay doğrulaması: `4` thumbnail
- Statik dosya doğrulaması: dört URL için HTTP `200`

### `src/SecureShop.Api/Data/Seed/CatalogSeeder.cs`

Uzantı: `.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using SecureShop.Api.Domain.Entities;

namespace SecureShop.Api.Data.Seed;

public sealed class CatalogSeeder
{
    private static readonly IReadOnlyList<ProductSeedDefinition> Products =
    [
        new(
            "Kablosuz Gürültü Engelleyici Kulaklık",
            "SSH-HEADPHONE-01",
            "Aktif gürültü engelleme, şeffaf mod ve 40 saate kadar pil ömrü sunan premium kablosuz kulaklık.",
            249.90m,
            24,
            "headphones",
            5),
        new(
            "Akıllı Spor Saati",
            "SSH-WATCH-01",
            "AMOLED ekran, GPS, sağlık takibi ve suya dayanıklı alüminyum gövdeli akıllı saat.",
            189.90m,
            31,
            "smartwatch",
            0),
        new(
            "RGB Mekanik Klavye",
            "SSH-KEYBOARD-01",
            "Hot-swap mekanik switch, RGB aydınlatma ve kompakt alüminyum kasaya sahip oyuncu klavyesi.",
            139.90m,
            18,
            "keyboard",
            0),
        new(
            "Taşınabilir Bluetooth Hoparlör",
            "SSH-SPEAKER-01",
            "360 derece ses, IP67 koruma ve 18 saat pil ömrü sunan taşınabilir Bluetooth hoparlör.",
            119.90m,
            42,
            "speaker",
            0),
        new(
            "4K Aksiyon Kamerası",
            "SSH-CAMERA-01",
            "4K video, gelişmiş görüntü sabitleme ve su geçirmez gövdeye sahip kompakt aksiyon kamerası.",
            329.90m,
            15,
            "SSH-CAMERA-01",
            4)
    ];

    private readonly AppDbContext _dbContext;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<CatalogSeeder> _logger;

    public CatalogSeeder(
        AppDbContext dbContext,
        IHostEnvironment environment,
        ILogger<CatalogSeeder> logger)
    {
        _dbContext = dbContext;
        _environment = environment;
        _logger = logger;
    }

    public async Task SeedAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_environment.IsDevelopment())
        {
            return;
        }

        var category = await _dbContext.Categories
            .SingleOrDefaultAsync(
                item => item.Name == "Elektronik",
                cancellationToken);

        if (category is null)
        {
            category = new Category("Elektronik");
            _dbContext.Categories.Add(category);
        }

        foreach (var definition in Products)
        {
            var product = await _dbContext.Products
                .Include(item => item.Images)
                .SingleOrDefaultAsync(
                    item => item.Sku == definition.Sku,
                    cancellationToken);

            if (product is null)
            {
                product = new Product(
                    category.Id,
                    definition.Name,
                    definition.Sku,
                    definition.Price,
                    definition.StockQuantity,
                    definition.Description);

                _dbContext.Products.Add(product);
            }

            var expectedImageUrls = Enumerable
                .Range(1, definition.ImageCount)
                .Select(index =>
                    $"/images/products/{definition.AssetPrefix}/{index}.png")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var obsoleteImage in product.Images
                .Where(image => !expectedImageUrls.Contains(image.ImageUrl))
                .ToList())
            {
                product.Images.Remove(obsoleteImage);
                _dbContext.ProductImages.Remove(obsoleteImage);
            }

            for (var index = 1; index <= definition.ImageCount; index++)
            {
                var sortOrder = index - 1;

                if (product.Images.Any(
                    image => image.SortOrder == sortOrder))
                {
                    continue;
                }

                product.AddImage(
                    $"/images/products/{definition.AssetPrefix}/{index}.png",
                    $"{definition.Name} - görünüm {index}",
                    sortOrder,
                    isPrimary: index == 1);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Development catalog synchronized with {ProductCount} products and {ImageCount} images.",
            Products.Count,
            Products.Sum(product => product.ImageCount));
    }

    private sealed record ProductSeedDefinition(
        string Name,
        string Sku,
        string Description,
        decimal Price,
        int StockQuantity,
        string AssetPrefix,
        int ImageCount);
}
```

## 8. Ürün detay URL'sinde SKU kullanımı

Ürün detay sayfaları GUID yerine okunabilir SKU ile açılır.

```text
Önce: https://localhost:7002/products/6fc1348c-0e47-44aa-98d5-26576c3631ac
Şimdi: https://localhost:7002/products/SSH-CAMERA-01
```

Eski GUID bağlantıları bozulmadı. GUID ile gelen istek ürünü API'den bulur ve
kalıcı olarak kullanılacak SKU adresine `302` yönlendirmesi yapar.

- Yeni API rotası: `GET /api/products/by-sku/{sku}`
- Yeni MVC rotası: `GET /products/{sku}`
- Katalog ürün kartları SKU bağlantısı üretir.
- Sepet ürün bağlantıları SKU kullanır.
- Admin görüntüleme bağlantısı SKU kullanır.
- Yeni ürün oluşturma sonrası SKU adresine yönlendirilir.
- Pasif veya bulunamayan SKU için `404` davranışı korunur.

Doğrulama:

- `/api/products/by-sku/SSH-CAMERA-01`: HTTP `200`
- `/products/SSH-CAMERA-01`: HTTP `200`
- Eski GUID adresi: HTTP `302`
- Yönlendirme hedefi: `/products/SSH-CAMERA-01`
- Release build: `0` uyarı, `0` hata

### Güncellenen dosyalar

- `src/SecureShop.Api/Features/Products/IProductService.cs` (`.cs`)
- `src/SecureShop.Api/Features/Products/ProductService.cs` (`.cs`)
- `src/SecureShop.Api/Controllers/ProductsController.cs` (`.cs`)
- `src/SecureShop.Mvc/Controllers/ProductsController.cs` (`.cs`)
- `src/SecureShop.Mvc/Services/Interfaces/IProductApiService.cs` (`.cs`)
- `src/SecureShop.Mvc/Services/Api/ProductApiService.cs` (`.cs`)
- `src/SecureShop.Mvc/Controllers/AdminProductsController.cs` (`.cs`)
- `src/SecureShop.Mvc/Views/Products/Index.cshtml` (`.cshtml`)
- `src/SecureShop.Mvc/Views/Cart/Index.cshtml` (`.cshtml`)
- `src/SecureShop.Mvc/Views/AdminProducts/Index.cshtml` (`.cshtml`)

### Tam kaynak kodları

#### `src/SecureShop.Api/Features/Products/IProductService.cs`

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

#### `src/SecureShop.Api/Features/Products/ProductService.cs`

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

#### `src/SecureShop.Api/Controllers/ProductsController.cs`

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

#### `src/SecureShop.Mvc/Controllers/ProductsController.cs`

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

#### `src/SecureShop.Mvc/Services/Interfaces/IProductApiService.cs`

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

#### `src/SecureShop.Mvc/Services/Api/ProductApiService.cs`

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

#### `src/SecureShop.Mvc/Controllers/AdminProductsController.cs`

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

#### `src/SecureShop.Mvc/Views/Products/Index.cshtml`

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

#### `src/SecureShop.Mvc/Views/Cart/Index.cshtml`

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

#### `src/SecureShop.Mvc/Views/AdminProducts/Index.cshtml`

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

<!-- CODE_APPEND -->
