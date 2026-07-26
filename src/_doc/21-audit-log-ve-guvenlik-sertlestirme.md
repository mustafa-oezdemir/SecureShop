# 1. Bu Adımın Amacı

Kritik ürün ve sipariş değişikliklerini hassas veri yazmadan izlenebilir hale getirmek, audit listesini yalnızca Admin rolüne açmak ve API/MVC yanıtlarına merkezi hata yönetimi ile güvenlik başlıkları eklemek.

# 2. Etkilenen Uygulama

```text
SecureShop.Api
SecureShop.Mvc
SQL Server
```

# 3. Bu Adımda Yapılacaklar

1. Append-only kullanım amaçlı AuditLog modelini eklemek.
2. Authenticated user, action, entity, IP ve allowlist detaylarını kaydetmek.
3. Ürün create/update/activate/deactivate olaylarını audit etmek.
4. Sipariş create/approve/ready/cancel/QR completion olaylarını audit etmek.
5. Audit sorgusunu AdminOnly policy ile korumak.
6. MVC admin audit ekranını typed API client ile eklemek.
7. ProblemDetails, exception handler ve güvenlik başlıklarını etkinleştirmek.

# 4. CLI Komutları

Çalışma dizini: ``D:\Code\ASP.NET\SecureShop``

```powershell
dotnet restore SecureShop.sln
dotnet build SecureShop.sln -c Release --no-restore
dotnet test SecureShop.sln -c Release --no-build
```

Veritabanı modeli içeren adımlarda:

```powershell
dotnet ef database update --project src\SecureShop.Api\SecureShop.Api.csproj --startup-project src\SecureShop.Api\SecureShop.Api.csproj --configuration Release
dotnet ef migrations has-pending-model-changes --project src\SecureShop.Api\SecureShop.Api.csproj --startup-project src\SecureShop.Api\SecureShop.Api.csproj --configuration Release
```

Terminal 1 — çalışma dizini: ``src\SecureShop.Api``

```powershell
dotnet watch run --launch-profile https
```

Terminal 2 — çalışma dizini: ``src\SecureShop.Mvc``

```powershell
dotnet watch run --launch-profile https
```

Çalışan eski ``dotnet watch`` süreci EXE dosyasını kilitlerse önce ilgili terminalde
``Ctrl+C`` kullanılmalıdır. Ardından iki uygulama yeniden başlatılmalıdır.

# 5. Güncel Proje Yapısı

```text
src/
├── SecureShop.Api/Domain/Entities/AuditLog.cs
├── SecureShop.Api/Features/Audit
├── SecureShop.Api/Controllers/AuditLogsController.cs
└── SecureShop.Mvc/{Controllers/AdminAuditController.cs,Services,Views/AdminAudit}
```

# 6. Dosya Bazında Eksiksiz Kodlar

Bu bölümdeki içerikler tamamlanmış çalışma ağacındaki dosyaların eksiksiz güncel
halleridir; ``...`` ile kısaltma yapılmamıştır.

## `src/SecureShop.Api/Domain/Entities/AuditLog.cs`

Uzantı: `.cs`

``csharp
namespace SecureShop.Api.Domain.Entities;

public sealed class AuditLog
{
    private AuditLog()
    {
    }

    public AuditLog(
        Guid? userId,
        string action,
        string entityType,
        string? entityId,
        string? detailsJson,
        string? ipAddress)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Action = Normalize(action, 100, nameof(action));
        EntityType = Normalize(
            entityType,
            100,
            nameof(entityType));
        EntityId = NormalizeOptional(entityId, 100);
        DetailsJson = NormalizeOptional(detailsJson, 4000);
        IpAddress = NormalizeOptional(ipAddress, 64);
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid? UserId { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public string EntityType { get; private set; } = string.Empty;

    public string? EntityId { get; private set; }

    public string? DetailsJson { get; private set; }

    public string? IpAddress { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    private static string Normalize(
        string value,
        int maximumLength,
        string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            value,
            parameterName);

        return NormalizeOptional(value, maximumLength)!;
    }

    private static string? NormalizeOptional(
        string? value,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();

        return normalized.Length <= maximumLength
            ? normalized
            : normalized[..maximumLength];
    }
}
``

## `src/SecureShop.Api/Data/Configurations/AuditLogConfiguration.cs`

Uzantı: `.cs`

``csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecureShop.Api.Domain.Entities;
using SecureShop.Api.Security.Identity;

namespace SecureShop.Api.Data.Configurations;

public sealed class AuditLogConfiguration
    : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(log => log.Id);

        builder.HasIndex(log => log.CreatedAtUtc)
            .HasDatabaseName("IX_AuditLogs_CreatedAtUtc");
        builder.HasIndex(log => new
            {
                log.EntityType,
                log.EntityId
            })
            .HasDatabaseName("IX_AuditLogs_Entity");

        builder.Property(log => log.Action)
            .HasMaxLength(100)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(log => log.EntityType)
            .HasMaxLength(100)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(log => log.EntityId)
            .HasMaxLength(100)
            .IsUnicode(false);
        builder.Property(log => log.DetailsJson)
            .HasMaxLength(4000);
        builder.Property(log => log.IpAddress)
            .HasMaxLength(64)
            .IsUnicode(false);
        builder.Property(log => log.CreatedAtUtc)
            .HasPrecision(0)
            .IsRequired();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(log => log.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
``

## `src/SecureShop.Api/Features/Audit/IAuditService.cs`

Uzantı: `.cs`

``csharp
namespace SecureShop.Api.Features.Audit;

public interface IAuditService
{
    void Record(
        string action,
        string entityType,
        string? entityId,
        object? details = null);
}
``

## `src/SecureShop.Api/Features/Audit/AuditService.cs`

Uzantı: `.cs`

``csharp
using System.Text.Json;
using SecureShop.Api.Data;
using SecureShop.Api.Domain.Entities;
using SecureShop.Api.Security;

namespace SecureShop.Api.Features.Audit;

public sealed class AuditService : IAuditService
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditService(
        AppDbContext dbContext,
        ICurrentUserService currentUser,
        IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _httpContextAccessor = httpContextAccessor;
    }

    public void Record(
        string action,
        string entityType,
        string? entityId,
        object? details = null)
    {
        var ipAddress = _httpContextAccessor
            .HttpContext?
            .Connection
            .RemoteIpAddress?
            .ToString();

        var detailsJson = details is null
            ? null
            : JsonSerializer.Serialize(details, SerializerOptions);

        _dbContext.AuditLogs.Add(new AuditLog(
            _currentUser.UserId,
            action,
            entityType,
            entityId,
            detailsJson,
            ipAddress));
    }
}
``

## `src/SecureShop.Api/Features/Audit/IAuditQueryService.cs`

Uzantı: `.cs`

``csharp
using SecureShop.Api.Contracts.Responses;

namespace SecureShop.Api.Features.Audit;

public interface IAuditQueryService
{
    Task<IReadOnlyList<AuditLogResponse>> GetLatestAsync(
        int take,
        CancellationToken cancellationToken);
}
``

## `src/SecureShop.Api/Features/Audit/AuditQueryService.cs`

Uzantı: `.cs`

``csharp
using Microsoft.EntityFrameworkCore;
using SecureShop.Api.Contracts.Responses;
using SecureShop.Api.Data;

namespace SecureShop.Api.Features.Audit;

public sealed class AuditQueryService : IAuditQueryService
{
    private readonly AppDbContext _dbContext;

    public AuditQueryService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<AuditLogResponse>>
        GetLatestAsync(
            int take,
            CancellationToken cancellationToken)
    {
        var safeTake = Math.Clamp(take, 1, 500);

        return await _dbContext.AuditLogs
            .AsNoTracking()
            .OrderByDescending(log => log.CreatedAtUtc)
            .Take(safeTake)
            .Select(log => new AuditLogResponse(
                log.Id,
                log.UserId,
                log.Action,
                log.EntityType,
                log.EntityId,
                log.DetailsJson,
                log.IpAddress,
                log.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }
}
``

## `src/SecureShop.Api/Contracts/Responses/AuditLogResponse.cs`

Uzantı: `.cs`

``csharp
namespace SecureShop.Api.Contracts.Responses;

public sealed record AuditLogResponse(
    Guid Id,
    Guid? UserId,
    string Action,
    string EntityType,
    string? EntityId,
    string? DetailsJson,
    string? IpAddress,
    DateTimeOffset CreatedAtUtc);
``

## `src/SecureShop.Api/Controllers/AuditLogsController.cs`

Uzantı: `.cs`

``csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureShop.Api.Contracts.Responses;
using SecureShop.Api.Features.Audit;
using SecureShop.Api.Security.Policies;

namespace SecureShop.Api.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize(Policy = AppPolicies.AdminOnly)]
public sealed class AuditLogsController : ControllerBase
{
    private readonly IAuditQueryService _auditQuery;

    public AuditLogsController(IAuditQueryService auditQuery)
    {
        _auditQuery = auditQuery;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AuditLogResponse>>> Get(
        [FromQuery] int take = 200,
        CancellationToken cancellationToken = default) =>
        Ok(await _auditQuery.GetLatestAsync(
            take,
            cancellationToken));
}
``

## `src/SecureShop.Api/Features/Products/ProductService.cs`

Uzantı: `.cs`

``csharp
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SecureShop.Api.Contracts.Requests;
using SecureShop.Api.Contracts.Responses;
using SecureShop.Api.Data;
using SecureShop.Api.Domain.Entities;
using SecureShop.Api.Features.Audit;

namespace SecureShop.Api.Features.Products;

public sealed class ProductService : IProductService
{
    private readonly AppDbContext _dbContext;
    private readonly IAuditService _audit;

    public ProductService(
        AppDbContext dbContext,
        IAuditService audit)
    {
        _dbContext = dbContext;
        _audit = audit;
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
        _audit.Record(
            "Product.Created",
            nameof(Product),
            product.Id.ToString("D"),
            new
            {
                product.Sku,
                product.Name,
                ImageCount = request.Images.Count
            });

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

        _audit.Record(
            "Product.Updated",
            nameof(Product),
            product.Id.ToString("D"),
            new
            {
                product.Sku,
                product.Name,
                AddedImageCount = request.Images.Count
            });

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

        _audit.Record(
            request.IsActive
                ? "Product.Activated"
                : "Product.Deactivated",
            nameof(Product),
            product.Id.ToString("D"),
            new
            {
                product.Sku
            });

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
``

## `src/SecureShop.Api/Features/Orders/OrderService.cs`

Uzantı: `.cs`

``csharp
using System.Data;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using SecureShop.Api.Contracts.Requests;
using SecureShop.Api.Contracts.Responses;
using SecureShop.Api.Data;
using SecureShop.Api.Domain.Entities;
using SecureShop.Api.Domain.Enums;
using SecureShop.Api.Features.Audit;
using SecureShop.Api.Features.QrCodes;

namespace SecureShop.Api.Features.Orders;

public sealed class OrderService : IOrderService
{
    private readonly AppDbContext _dbContext;
    private readonly IAuditService _audit;
    private readonly IOrderQrTokenService _qrTokenService;
    private readonly IQrCodeGenerator _qrCodeGenerator;
    private readonly OrderQrOptions _qrOptions;

    public OrderService(
        AppDbContext dbContext,
        IAuditService audit,
        IOrderQrTokenService qrTokenService,
        IQrCodeGenerator qrCodeGenerator,
        IOptions<OrderQrOptions> qrOptions)
    {
        _dbContext = dbContext;
        _audit = audit;
        _qrTokenService = qrTokenService;
        _qrCodeGenerator = qrCodeGenerator;
        _qrOptions = qrOptions.Value;
    }

    public async Task<OrderMutationResult> CreateAsync(
        Guid userId,
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        IDbContextTransaction? transaction = null;

        if (_dbContext.Database.IsRelational())
        {
            transaction = await _dbContext.Database
                .BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);
        }

        await using (transaction)
        {
            var cart = await _dbContext.Carts
                .Include(currentCart => currentCart.Items)
                    .ThenInclude(item => item.Product)
                        .ThenInclude(product => product.Category)
                .AsSplitQuery()
                .SingleOrDefaultAsync(
                    currentCart => currentCart.UserId == userId,
                    cancellationToken);

            if (cart is null || cart.Items.Count == 0)
            {
                return new(OrderMutationStatus.CartEmpty);
            }

            foreach (var item in cart.Items)
            {
                if (!item.Product.IsActive
                    || !item.Product.Category.IsActive)
                {
                    return new(
                        OrderMutationStatus.ProductUnavailable);
                }

                if (item.Quantity > item.Product.StockQuantity)
                {
                    return new(
                        OrderMutationStatus.InsufficientStock);
                }
            }

            var order = new Order(
                userId,
                CreateOrderNumber(),
                request.RecipientName,
                request.AddressLine,
                request.PostalCode,
                request.City,
                request.Country);

            foreach (var item in cart.Items)
            {
                order.AddItem(
                    item.ProductId,
                    item.Product.Name,
                    item.Product.Sku,
                    item.Product.Price,
                    item.Quantity);

                item.Product.DecreaseStock(item.Quantity);
            }

            cart.Clear();
            _dbContext.Orders.Add(order);

            _audit.Record(
                "Order.Created",
                nameof(Order),
                order.Id.ToString("D"),
                new
                {
                    order.OrderNumber,
                    order.TotalAmount,
                    ItemCount = order.Items.Count
                });

            try
            {
                await _dbContext.SaveChangesAsync(
                    cancellationToken);

                if (transaction is not null)
                {
                    await transaction.CommitAsync(
                        cancellationToken);
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(
                        cancellationToken);
                }

                return new(
                    OrderMutationStatus.ConcurrencyConflict);
            }
            catch (DbUpdateException)
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(
                        cancellationToken);
                }

                return new(
                    OrderMutationStatus.ConcurrencyConflict);
            }

            var createdOrder = await GetCustomerOrderAsync(
                userId,
                order.OrderNumber,
                cancellationToken);

            return new(
                OrderMutationStatus.Succeeded,
                createdOrder);
        }
    }

    public async Task<IReadOnlyList<OrderResponse>>
        GetCustomerOrdersAsync(
            Guid userId,
            CancellationToken cancellationToken)
    {
        var orders = await BaseOrderQuery(tracking: false)
            .Where(order => order.UserId == userId)
            .OrderByDescending(order => order.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return orders
            .Select(order => Map(order, includeQr: false))
            .ToList();
    }

    public async Task<OrderResponse?> GetCustomerOrderAsync(
        Guid userId,
        string orderNumber,
        CancellationToken cancellationToken)
    {
        var normalizedOrderNumber =
            NormalizeOrderNumber(orderNumber);

        var order = await BaseOrderQuery(tracking: false)
            .SingleOrDefaultAsync(
                currentOrder =>
                    currentOrder.UserId == userId
                    && currentOrder.OrderNumber
                        == normalizedOrderNumber,
                cancellationToken);

        return order is null
            ? null
            : Map(order, includeQr: true);
    }

    public async Task<IReadOnlyList<OrderResponse>>
        GetStaffOrdersAsync(
            CancellationToken cancellationToken)
    {
        var orders = await BaseOrderQuery(tracking: false)
            .OrderByDescending(order => order.CreatedAtUtc)
            .Take(250)
            .ToListAsync(cancellationToken);

        return orders
            .Select(order => Map(order, includeQr: false))
            .ToList();
    }

    public async Task<OrderResponse?> GetStaffOrderAsync(
        string orderNumber,
        CancellationToken cancellationToken)
    {
        var normalizedOrderNumber =
            NormalizeOrderNumber(orderNumber);

        var order = await BaseOrderQuery(tracking: false)
            .SingleOrDefaultAsync(
                currentOrder => currentOrder.OrderNumber
                    == normalizedOrderNumber,
                cancellationToken);

        return order is null
            ? null
            : Map(order, includeQr: true);
    }

    public Task<OrderMutationResult> ApproveAsync(
        string orderNumber,
        Guid staffUserId,
        string rowVersion,
        CancellationToken cancellationToken) =>
        ProcessAsync(
            orderNumber,
            staffUserId,
            rowVersion,
            "Order.Approved",
            order => order.Approve(staffUserId),
            restoreStock: false,
            cancellationToken);

    public Task<OrderMutationResult> MarkReadyAsync(
        string orderNumber,
        Guid staffUserId,
        string rowVersion,
        CancellationToken cancellationToken) =>
        ProcessAsync(
            orderNumber,
            staffUserId,
            rowVersion,
            "Order.ReadyForPickup",
            order => order.MarkReadyForPickup(staffUserId),
            restoreStock: false,
            cancellationToken);

    public Task<OrderMutationResult> CancelAsync(
        string orderNumber,
        Guid staffUserId,
        string rowVersion,
        CancellationToken cancellationToken) =>
        ProcessAsync(
            orderNumber,
            staffUserId,
            rowVersion,
            "Order.Cancelled",
            order => order.Cancel(staffUserId),
            restoreStock: true,
            cancellationToken);

    public async Task<OrderMutationResult> CompleteByQrAsync(
        string token,
        Guid staffUserId,
        CancellationToken cancellationToken)
    {
        if (!_qrTokenService.TryValidate(token, out var orderId))
        {
            return new(OrderMutationStatus.InvalidQrCode);
        }

        var order = await BaseOrderQuery(tracking: true)
            .SingleOrDefaultAsync(
                currentOrder => currentOrder.Id == orderId,
                cancellationToken);

        if (order is null)
        {
            return new(OrderMutationStatus.NotFound);
        }

        try
        {
            order.Complete(staffUserId);
        }
        catch (InvalidOperationException)
        {
            return new(OrderMutationStatus.InvalidTransition);
        }

        _audit.Record(
            "Order.CompletedByQr",
            nameof(Order),
            order.Id.ToString("D"),
            new
            {
                order.OrderNumber
            });

        return await SaveProcessedOrderAsync(
            order,
            cancellationToken);
    }

    private async Task<OrderMutationResult> ProcessAsync(
        string orderNumber,
        Guid staffUserId,
        string rowVersion,
        string auditAction,
        Action<Order> transition,
        bool restoreStock,
        CancellationToken cancellationToken)
    {
        var normalizedOrderNumber =
            NormalizeOrderNumber(orderNumber);

        var order = await BaseOrderQuery(tracking: true)
            .SingleOrDefaultAsync(
                currentOrder => currentOrder.OrderNumber
                    == normalizedOrderNumber,
                cancellationToken);

        if (order is null)
        {
            return new(OrderMutationStatus.NotFound);
        }

        if (!TryDecodeRowVersion(rowVersion, out var version))
        {
            return new(OrderMutationStatus.InvalidRowVersion);
        }

        _dbContext.Entry(order)
            .Property(currentOrder => currentOrder.RowVersion)
            .OriginalValue = version;

        try
        {
            transition(order);
        }
        catch (InvalidOperationException)
        {
            return new(OrderMutationStatus.InvalidTransition);
        }

        if (restoreStock)
        {
            foreach (var item in order.Items)
            {
                item.Product.IncreaseStock(item.Quantity);
            }
        }

        _audit.Record(
            auditAction,
            nameof(Order),
            order.Id.ToString("D"),
            new
            {
                order.OrderNumber,
                Status = order.Status.ToString()
            });

        return await SaveProcessedOrderAsync(
            order,
            cancellationToken);
    }

    private async Task<OrderMutationResult> SaveProcessedOrderAsync(
        Order order,
        CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(OrderMutationStatus.ConcurrencyConflict);
        }

        var response = await GetStaffOrderAsync(
            order.OrderNumber,
            cancellationToken);

        return new(
            OrderMutationStatus.Succeeded,
            response);
    }

    private IQueryable<Order> BaseOrderQuery(bool tracking)
    {
        var query = _dbContext.Orders
            .Include(order => order.Items)
                .ThenInclude(item => item.Product)
            .AsSplitQuery();

        return tracking ? query : query.AsNoTracking();
    }

    private OrderResponse Map(
        Order order,
        bool includeQr)
    {
        string? qrCodeDataUrl = null;

        if (includeQr
            && order.Status is OrderStatus.Approved
                or OrderStatus.ReadyForPickup)
        {
            var token = _qrTokenService.Generate(order.Id);
            var verificationUrl = QueryHelpers.AddQueryString(
                _qrOptions.VerificationBaseUrl,
                "token",
                token);

            qrCodeDataUrl = _qrCodeGenerator.GeneratePngDataUrl(
                verificationUrl);
        }

        return new OrderResponse(
            order.Id,
            order.OrderNumber,
            order.UserId,
            order.RecipientName,
            order.AddressLine,
            order.PostalCode,
            order.City,
            order.Country,
            order.Status.ToString(),
            order.TotalAmount,
            order.Items
                .OrderBy(item => item.ProductName)
                .Select(item => new OrderItemResponse(
                    item.ProductId,
                    item.ProductName,
                    item.Sku,
                    item.UnitPrice,
                    item.Quantity,
                    item.LineTotal))
                .ToList(),
            order.CreatedAtUtc,
            order.UpdatedAtUtc,
            order.CompletedAtUtc,
            Convert.ToBase64String(order.RowVersion),
            qrCodeDataUrl);
    }

    private static string CreateOrderNumber() =>
        $"SSH-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..21]
            .ToUpperInvariant();

    private static string NormalizeOrderNumber(
        string orderNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderNumber);
        return orderNumber.Trim().ToUpperInvariant();
    }

    private static bool TryDecodeRowVersion(
        string value,
        out byte[] rowVersion)
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
}
``

## `src/SecureShop.Api/Program.cs`

Uzantı: `.cs`

``csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using SecureShop.Api.Configuration;
using SecureShop.Api.Data;
using SecureShop.Api.Data.Seed;
using SecureShop.Api.Domain.Constants;
using SecureShop.Api.Features.Auth.External;
using SecureShop.Api.Features.Auth.TwoFactor;
using SecureShop.Api.Features.Audit;
using SecureShop.Api.Features.Cart;
using SecureShop.Api.Features.Orders;
using SecureShop.Api.Features.Products;
using SecureShop.Api.Features.QrCodes;
using SecureShop.Api.Security;
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

builder.Services
    .AddOptions<OrderQrOptions>()
    .Bind(builder.Configuration.GetSection(
        OrderQrOptions.SectionName))
    .Validate(
        options =>
            Uri.TryCreate(
                options.VerificationBaseUrl,
                UriKind.Absolute,
                out var uri)
            && uri.Scheme == Uri.UriSchemeHttps,
        "QR doğrulama adresi geçerli bir HTTPS adresi olmalıdır.")
    .Validate(
        options => options.LifetimeMinutes is >= 5 and <= 525_600,
        "QR token süresi 5 ile 525600 dakika arasında olmalıdır.")
    .ValidateOnStart();

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

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IdentitySeeder>();
builder.Services.AddScoped<CatalogSeeder>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IAuditQueryService, AuditQueryService>();
builder.Services.AddSingleton<IQrCodeGenerator, PngQrCodeGenerator>();
builder.Services.AddSingleton<IOrderQrTokenService, OrderQrTokenService>();

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
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

    var catalogSeeder =
        scope.ServiceProvider.GetRequiredService<CatalogSeeder>();

    await catalogSeeder.SeedAsync();
}
else
{
    app.UseHsts();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers.ContentSecurityPolicy =
        "default-src 'none'; frame-ancestors 'none';";

    await next();
});

app.UseRouting();
app.UseRateLimiter();

app.UseCookiePolicy();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await app.RunAsync();

public partial class Program
{
}
``

## `src/SecureShop.Mvc/Models/Responses/AuditLogResponse.cs`

Uzantı: `.cs`

``csharp
namespace SecureShop.Mvc.Models.Responses;

public sealed record AuditLogResponse(
    Guid Id,
    Guid? UserId,
    string Action,
    string EntityType,
    string? EntityId,
    string? DetailsJson,
    string? IpAddress,
    DateTimeOffset CreatedAtUtc);
``

## `src/SecureShop.Mvc/Services/Interfaces/IAuditApiService.cs`

Uzantı: `.cs`

``csharp
using SecureShop.Mvc.Http;
using SecureShop.Mvc.Models.Responses;

namespace SecureShop.Mvc.Services.Interfaces;

public interface IAuditApiService
{
    Task<ApiResponse<IReadOnlyList<AuditLogResponse>>> GetLatestAsync(
        int take = 200,
        CancellationToken cancellationToken = default);
}
``

## `src/SecureShop.Mvc/Services/Api/AuditApiService.cs`

Uzantı: `.cs`

``csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SecureShop.Mvc.Http;
using SecureShop.Mvc.Models.Responses;
using SecureShop.Mvc.Services.Interfaces;

namespace SecureShop.Mvc.Services.Api;

public sealed class AuditApiService : IAuditApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AuditApiService> _logger;

    public AuditApiService(
        HttpClient httpClient,
        ILogger<AuditApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ApiResponse<IReadOnlyList<AuditLogResponse>>>
        GetLatestAsync(
            int take = 200,
            CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(
                $"api/audit-logs?take={Math.Clamp(take, 1, 500)}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ApiResponse<IReadOnlyList<AuditLogResponse>>
                    .Failure(
                        response.StatusCode,
                        "Audit kayıtları alınamadı.");
            }

            var logs = await response.Content
                .ReadFromJsonAsync<List<AuditLogResponse>>(
                    cancellationToken: cancellationToken);

            return logs is null
                ? ApiResponse<IReadOnlyList<AuditLogResponse>>
                    .Failure(
                        HttpStatusCode.BadGateway,
                        "API geçerli audit response'u döndürmedi.")
                : ApiResponse<IReadOnlyList<AuditLogResponse>>
                    .Success(response.StatusCode, logs);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                exception,
                "Audit API isteği tamamlanamadı.");

            return ApiResponse<IReadOnlyList<AuditLogResponse>>
                .Failure(
                    HttpStatusCode.ServiceUnavailable,
                    "SecureShop API hizmetine ulaşılamıyor.");
        }
        catch (JsonException exception)
        {
            _logger.LogError(
                exception,
                "Audit API response'u okunamadı.");

            return ApiResponse<IReadOnlyList<AuditLogResponse>>
                .Failure(
                    HttpStatusCode.BadGateway,
                    "Audit response formatı geçersiz.");
        }
    }
}
``

## `src/SecureShop.Mvc/Controllers/AdminAuditController.cs`

Uzantı: `.cs`

``csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureShop.Mvc.Security;
using SecureShop.Mvc.Services.Interfaces;

namespace SecureShop.Mvc.Controllers;

[Authorize(Roles = AppRoles.Admin)]
[Route("admin/audit")]
public sealed class AdminAuditController : Controller
{
    private readonly IAuditApiService _auditApiService;

    public AdminAuditController(
        IAuditApiService auditApiService)
    {
        _auditApiService = auditApiService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken)
    {
        var result = await _auditApiService.GetLatestAsync(
            cancellationToken: cancellationToken);

        if (!result.IsSuccess)
        {
            ViewData["ErrorMessage"] = result.ErrorMessage;
        }

        return View(result.Data ?? []);
    }
}
``

## `src/SecureShop.Mvc/Views/AdminAudit/Index.cshtml`

Uzantı: `.cshtml`

``cshtml
@model IReadOnlyList<SecureShop.Mvc.Models.Responses.AuditLogResponse>
@{
    ViewData["Title"] = "Audit kayıtları";
}

<div class="mb-4">
    <span class="text-uppercase text-primary fw-semibold small">Admin güvenlik</span>
    <h1 class="mb-0">Audit kayıtları</h1>
</div>

@if (ViewData["ErrorMessage"] is string errorMessage)
{
    <div class="alert alert-danger">@errorMessage</div>
}
else
{
    <div class="card border-0 shadow-sm">
        <div class="table-responsive">
            <table class="table table-sm align-middle mb-0 audit-table">
                <thead>
                    <tr>
                        <th>Zaman</th>
                        <th>İşlem</th>
                        <th>Varlık</th>
                        <th>Kullanıcı</th>
                        <th>IP</th>
                        <th>Detay</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach (var log in Model)
                    {
                        <tr>
                            <td class="text-nowrap">@log.CreatedAtUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss")</td>
                            <td><code>@log.Action</code></td>
                            <td>@log.EntityType<br /><small>@log.EntityId</small></td>
                            <td><small>@log.UserId</small></td>
                            <td>@log.IpAddress</td>
                            <td><pre class="audit-details">@log.DetailsJson</pre></td>
                        </tr>
                    }
                </tbody>
            </table>
        </div>
    </div>
}
``

## `src/SecureShop.Mvc/Program.cs`

Uzantı: `.cs`

``csharp
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
``

## `src/SecureShop.Mvc/Views/Shared/_AuthenticationMenu.cshtml`

Uzantı: `.cshtml`

``cshtml
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
                @if (User.IsInRole(AppRoles.Customer))
                {
                    <li>
                        <a class="dropdown-item" asp-controller="Orders" asp-action="Index">
                            Siparişlerim
                        </a>
                    </li>
                }
                @if (User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Employee))
                {
                    <li>
                        <a class="dropdown-item" asp-controller="EmployeeOrders" asp-action="Index">
                            Sipariş yönetimi
                        </a>
                    </li>
                    <li>
                        <a class="dropdown-item" asp-controller="EmployeeOrders" asp-action="Verify">
                            QR doğrula
                        </a>
                    </li>
                }
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
                    <li>
                        <a class="dropdown-item" asp-controller="AdminAudit" asp-action="Index">
                            Audit kayıtları
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
``

## `src/SecureShop.Mvc/wwwroot/css/site.css`

Uzantı: `.css`

``css
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
.order-card { border-radius: 1rem; }
.order-summary-card { top: 1.5rem; }
.order-qr-image { width: min(100%, 18rem); height: auto; image-rendering: crisp-edges; }
.audit-table th, .audit-table td { padding: .75rem; vertical-align: top; }
.audit-details { max-width: 28rem; margin: 0; white-space: pre-wrap; overflow-wrap: anywhere; font-size: .75rem; }

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
``

# 7. Kod Açıklaması

Audit kayıtları iş mutasyonuyla aynı `AppDbContext` ve `SaveChanges` içinde yazılır; böylece başarısız işlem için yanıltıcı başarı audit'i oluşmaz. Ayrıntılar yalnızca çağıran servisin açıkça seçtiği güvenli alanlardan JSON üretilir; parola, cookie ve QR token kaydedilmez. Liste maksimum 500 kayıtla sınırlıdır ve yalnızca `AdminOnly` policy üzerinden erişilir. API ve MVC, MIME sniffing, referrer, framing ve CSP risklerini azaltan başlıklar döndürür.

# 8. API–MVC Veri Akışı

```text
Admin Audit Razor View
    ↓
AdminAuditController
    ↓
IAuditApiService / HttpClient
    ↓
GET /api/admin/audit (AdminOnly)
    ↓
AuditQueryService
    ↓
AppDbContext.AuditLogs
    ↓
SQL Server
```

MVC yalnızca request/response modelleri ve typed ``HttpClient`` servisleri kullanır.
Kullanıcı kimliği, rol, fiyat, stok ve toplam tutar API authentication context'i ile
SQL Server verilerinden belirlenir.

# 9. Uygulama Sırası

1. Domain entity ve enum sınıflarını oluştur.
2. EF Core configuration ve ``AppDbContext`` değişikliklerini uygula.
3. API request/response contract'larını oluştur.
4. API servislerini ve controller endpoint'lerini ekle.
5. MVC request/response/ViewModel sınıflarını ekle.
6. Typed API servislerini ve MVC controller'larını ekle.
7. Razor View ve navbar bağlantılarını ekle.
8. Migration'ı uygula, build ve test komutlarını çalıştır.

# 10. Çalıştırma ve Test

API ve MVC yukarıdaki iki ayrı terminalde çalıştırılır. Ardından:

```text
API: https://localhost:7001
MVC: https://localhost:7002
```

Otomatik doğrulama:

```powershell
dotnet test SecureShop.sln -c Release
```

Bu uygulamada doğrulanan gerçek akış:

```text
Customer login → ürün → sepet → checkout → stok düşümü
Employee login → sipariş onayı → QR üretimi → iptal → stok iadesi
Admin login → audit log listesi
```

# 11. Beklenen Sonuç

Kritik ürün/sipariş işlemlerinden sonra audit satırı oluşur. Admin son olayları kullanıcı, action, entity, IP ve güvenli detaylarla görebilir. Customer ve Employee audit endpoint'ine erişemez. API/MVC yanıtları tanımlı güvenlik başlıklarını içerir.

# 12. Yaygın Hatalar

- Audit içine secret/token/parola eklenmemelidir.
- Audit kayıtları normal kullanıcıya açılmamalıdır.
- CSP'ye inline script eklenirse tarayıcı engeller; script statik dosyaya taşınmalıdır.
- Production'da exception detayları kullanıcıya gösterilmez.

- ``localhost:7001 connection refused``: API çalışmıyordur veya MVC
  ``ApiSettings:BaseUrl`` yanlış portu gösteriyordur.
- ``MSB3027/MSB3021 apphost.exe locked``: eski ``dotnet run/watch`` sürecini
  ``Ctrl+C`` ile kapatıp yeniden build alın.
- ``401``: authentication cookie yoktur veya ortak Data Protection key-ring ayarı
  iki uygulamada aynı değildir.
- ``403``: kullanıcı gerekli role/policy'ye sahip değildir.
- ``409``: sipariş durumu ya da ``RowVersion`` başka işlem tarafından değişmiştir;
  güncel detay yeniden yüklenmelidir.

# 13. Tamamlama Kontrol Listesi

```text
[x] CLI komutları doğru klasörde çalıştırıldı
[x] Dosyalar doğru projede oluşturuldu
[x] Web API hatasız derlendi
[x] MVC uygulaması hatasız derlendi
[x] MVC, Web API'ye başarıyla bağlandı
[x] MVC doğrudan veritabanına erişmiyor
[x] Web API veritabanı işlemini başarıyla gerçekleştirdi
[x] Migration uygulandı ve bekleyen model değişikliği yok
[x] Güvenlik ve rol kontrolleri doğrulandı
[x] Otomatik testlerin tamamı geçti
[x] İstenen özellik uçtan uca doğrulandı
```