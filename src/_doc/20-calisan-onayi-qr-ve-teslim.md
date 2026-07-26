# 1. Bu Adımın Amacı

Admin/Employee personelinin siparişleri görev ayrımıyla işlemesini, durum geçişlerini optimistic concurrency ile korumayı ve süreli, kurcalamaya dayanıklı QR doğrulamasıyla teslimi tamamlamayı sağlamak.

# 2. Etkilenen Uygulama

```text
SecureShop.Api
SecureShop.Mvc
SQL Server
```

# 3. Bu Adımda Yapılacaklar

1. PendingApproval → Approved → ReadyForPickup → Completed durum makinesini uygulamak.
2. Admin ve Employee için StaffOnly policy kullanmak.
3. RowVersion ile çift işlem/çakışma önlemek.
4. Data Protection ile süreli ve imzalı QR token üretmek.
5. QR içeriğine ham order ID yerine HTTPS doğrulama URL'si koymak.
6. Personel liste, detay ve QR doğrulama ekranlarını eklemek.
7. İptalde stoğu API üzerinden geri yüklemek.

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
├── SecureShop.Api/Features/QrCodes
├── SecureShop.Api/Controllers/EmployeeOrdersController.cs
├── SecureShop.Api/Features/Orders/OrderService.cs
└── SecureShop.Mvc/{Controllers,Models,Services,Views/EmployeeOrders}
```

# 6. Dosya Bazında Eksiksiz Kodlar

Bu bölümdeki içerikler tamamlanmış çalışma ağacındaki dosyaların eksiksiz güncel
halleridir; ``...`` ile kısaltma yapılmamıştır.

## `src/SecureShop.Api/Contracts/Requests/ProcessOrderRequest.cs`

Uzantı: `.cs`

``csharp
using System.ComponentModel.DataAnnotations;

namespace SecureShop.Api.Contracts.Requests;

public sealed class ProcessOrderRequest
{
    [Required]
    public string RowVersion { get; init; } = string.Empty;
}
``

## `src/SecureShop.Api/Contracts/Requests/VerifyOrderQrRequest.cs`

Uzantı: `.cs`

``csharp
using System.ComponentModel.DataAnnotations;

namespace SecureShop.Api.Contracts.Requests;

public sealed class VerifyOrderQrRequest
{
    [Required]
    [StringLength(4000, MinimumLength = 20)]
    public string Token { get; init; } = string.Empty;
}
``

## `src/SecureShop.Api/Features/QrCodes/IQrCodeGenerator.cs`

Uzantı: `.cs`

``csharp
namespace SecureShop.Api.Features.QrCodes;

public interface IQrCodeGenerator
{
    string GeneratePngDataUrl(string content);
}
``

## `src/SecureShop.Api/Features/QrCodes/PngQrCodeGenerator.cs`

Uzantı: `.cs`

``csharp
using QRCoder;

namespace SecureShop.Api.Features.QrCodes;

public sealed class PngQrCodeGenerator : IQrCodeGenerator
{
    private const int PixelsPerModule = 8;

    public string GeneratePngDataUrl(string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        using var qrCodeData =
            QRCodeGenerator.GenerateQrCode(
                content,
                QRCodeGenerator.ECCLevel.Q);

        using var qrCode =
            new PngByteQRCode(qrCodeData);

        var pngBytes = qrCode.GetGraphic(
            PixelsPerModule,
            drawQuietZones: true);

        return $"data:image/png;base64,{Convert.ToBase64String(pngBytes)}";
    }
}
``

## `src/SecureShop.Api/Features/QrCodes/OrderQrOptions.cs`

Uzantı: `.cs`

``csharp
namespace SecureShop.Api.Features.QrCodes;

public sealed class OrderQrOptions
{
    public const string SectionName = "QrCodes:Orders";

    public string VerificationBaseUrl { get; set; } =
        "https://localhost:7002/employee/orders/verify";

    public int LifetimeMinutes { get; set; } = 43_200;
}
``

## `src/SecureShop.Api/Features/QrCodes/IOrderQrTokenService.cs`

Uzantı: `.cs`

``csharp
namespace SecureShop.Api.Features.QrCodes;

public interface IOrderQrTokenService
{
    string Generate(Guid orderId);

    bool TryValidate(string token, out Guid orderId);
}
``

## `src/SecureShop.Api/Features/QrCodes/OrderQrTokenService.cs`

Uzantı: `.cs`

``csharp
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace SecureShop.Api.Features.QrCodes;

public sealed class OrderQrTokenService : IOrderQrTokenService
{
    private readonly ITimeLimitedDataProtector _protector;
    private readonly TimeSpan _lifetime;

    public OrderQrTokenService(
        IDataProtectionProvider dataProtectionProvider,
        IOptions<OrderQrOptions> options)
    {
        _protector = dataProtectionProvider
            .CreateProtector(
                "SecureShop.Orders.QrVerification.v1")
            .ToTimeLimitedDataProtector();

        _lifetime = TimeSpan.FromMinutes(
            options.Value.LifetimeMinutes);
    }

    public string Generate(Guid orderId)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException(
                "Sipariş kimliği boş olamaz.",
                nameof(orderId));
        }

        return _protector.Protect(
            orderId.ToString("N"),
            _lifetime);
    }

    public bool TryValidate(string token, out Guid orderId)
    {
        orderId = Guid.Empty;

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            var value = _protector.Unprotect(
                token.Trim(),
                out _);

            return Guid.TryParseExact(value, "N", out orderId);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
``

## `src/SecureShop.Api/Domain/Entities/Order.cs`

Uzantı: `.cs`

``csharp
using SecureShop.Api.Domain.Enums;

namespace SecureShop.Api.Domain.Entities;

public sealed class Order
{
    private Order()
    {
    }

    public Order(
        Guid userId,
        string orderNumber,
        string recipientName,
        string addressLine,
        string postalCode,
        string city,
        string country)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "Kullanıcı kimliği boş olamaz.",
                nameof(userId));
        }

        Id = Guid.NewGuid();
        UserId = userId;
        OrderNumber = NormalizeRequired(
            orderNumber,
            32,
            nameof(orderNumber));
        RecipientName = NormalizeRequired(
            recipientName,
            200,
            nameof(recipientName));
        AddressLine = NormalizeRequired(
            addressLine,
            500,
            nameof(addressLine));
        PostalCode = NormalizeRequired(
            postalCode,
            20,
            nameof(postalCode));
        City = NormalizeRequired(city, 100, nameof(city));
        Country = NormalizeRequired(country, 100, nameof(country));
        Status = OrderStatus.PendingApproval;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string OrderNumber { get; private set; } = string.Empty;

    public string RecipientName { get; private set; } = string.Empty;

    public string AddressLine { get; private set; } = string.Empty;

    public string PostalCode { get; private set; } = string.Empty;

    public string City { get; private set; } = string.Empty;

    public string Country { get; private set; } = string.Empty;

    public OrderStatus Status { get; private set; }

    public decimal TotalAmount { get; private set; }

    public Guid? ProcessedByUserId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public ICollection<OrderItem> Items { get; private set; } =
        new List<OrderItem>();

    public void AddItem(
        Guid productId,
        string productName,
        string sku,
        decimal unitPrice,
        int quantity)
    {
        if (Status != OrderStatus.PendingApproval)
        {
            throw new InvalidOperationException(
                "Yalnızca bekleyen siparişe ürün eklenebilir.");
        }

        if (Items.Any(item => item.ProductId == productId))
        {
            throw new InvalidOperationException(
                "Aynı ürün siparişe iki kez eklenemez.");
        }

        Items.Add(new OrderItem(
            Id,
            productId,
            productName,
            sku,
            unitPrice,
            quantity));

        RecalculateTotal();
    }

    public void Approve(Guid staffUserId)
    {
        EnsureStaffUser(staffUserId);
        EnsureStatus(OrderStatus.PendingApproval);
        Status = OrderStatus.Approved;
        ProcessedByUserId = staffUserId;
        Touch();
    }

    public void MarkReadyForPickup(Guid staffUserId)
    {
        EnsureStaffUser(staffUserId);
        EnsureStatus(OrderStatus.Approved);
        Status = OrderStatus.ReadyForPickup;
        ProcessedByUserId = staffUserId;
        Touch();
    }

    public void Complete(Guid staffUserId)
    {
        EnsureStaffUser(staffUserId);
        EnsureStatus(OrderStatus.ReadyForPickup);
        Status = OrderStatus.Completed;
        ProcessedByUserId = staffUserId;
        CompletedAtUtc = DateTimeOffset.UtcNow;
        Touch();
    }

    public void Cancel(Guid staffUserId)
    {
        EnsureStaffUser(staffUserId);

        if (Status is not OrderStatus.PendingApproval
            and not OrderStatus.Approved)
        {
            throw new InvalidOperationException(
                "Bu sipariş artık iptal edilemez.");
        }

        Status = OrderStatus.Cancelled;
        ProcessedByUserId = staffUserId;
        Touch();
    }

    private void RecalculateTotal()
    {
        TotalAmount = decimal.Round(
            Items.Sum(item => item.LineTotal),
            2,
            MidpointRounding.ToEven);
    }

    private void EnsureStatus(OrderStatus expected)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException(
                $"Sipariş durumu '{expected}' olmalıdır.");
        }
    }

    private static void EnsureStaffUser(Guid staffUserId)
    {
        if (staffUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "Personel kimliği boş olamaz.",
                nameof(staffUserId));
        }
    }

    private void Touch()
    {
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private static string NormalizeRequired(
        string value,
        int maximumLength,
        string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            value,
            parameterName);

        var normalized = value.Trim();

        if (normalized.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"Değer {maximumLength} karakterden uzun olamaz.");
        }

        return normalized;
    }
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

## `src/SecureShop.Api/Controllers/EmployeeOrdersController.cs`

Uzantı: `.cs`

``csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureShop.Api.Contracts.Requests;
using SecureShop.Api.Contracts.Responses;
using SecureShop.Api.Features.Orders;
using SecureShop.Api.Security;
using SecureShop.Api.Security.Policies;

namespace SecureShop.Api.Controllers;

[ApiController]
[Route("api/employee/orders")]
[Authorize(Policy = AppPolicies.StaffOnly)]
public sealed class EmployeeOrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly ICurrentUserService _currentUser;

    public EmployeeOrdersController(
        IOrderService orderService,
        ICurrentUserService currentUser)
    {
        _orderService = orderService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrderResponse>>> GetAll(
        CancellationToken cancellationToken) =>
        Ok(await _orderService.GetStaffOrdersAsync(
            cancellationToken));

    [HttpGet("{orderNumber}")]
    public async Task<ActionResult<OrderResponse>> GetByNumber(
        string orderNumber,
        CancellationToken cancellationToken)
    {
        var order = await _orderService.GetStaffOrderAsync(
            orderNumber,
            cancellationToken);

        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost("{orderNumber}/approve")]
    public Task<ActionResult<OrderResponse>> Approve(
        string orderNumber,
        ProcessOrderRequest request,
        CancellationToken cancellationToken) =>
        ProcessAsync(
            (staffUserId, token) => _orderService.ApproveAsync(
                orderNumber,
                staffUserId,
                request.RowVersion,
                token),
            cancellationToken);

    [HttpPost("{orderNumber}/ready")]
    public Task<ActionResult<OrderResponse>> MarkReady(
        string orderNumber,
        ProcessOrderRequest request,
        CancellationToken cancellationToken) =>
        ProcessAsync(
            (staffUserId, token) => _orderService.MarkReadyAsync(
                orderNumber,
                staffUserId,
                request.RowVersion,
                token),
            cancellationToken);

    [HttpPost("{orderNumber}/cancel")]
    public Task<ActionResult<OrderResponse>> Cancel(
        string orderNumber,
        ProcessOrderRequest request,
        CancellationToken cancellationToken) =>
        ProcessAsync(
            (staffUserId, token) => _orderService.CancelAsync(
                orderNumber,
                staffUserId,
                request.RowVersion,
                token),
            cancellationToken);

    [HttpPost("verify-qr")]
    public Task<ActionResult<OrderResponse>> VerifyQr(
        VerifyOrderQrRequest request,
        CancellationToken cancellationToken) =>
        ProcessAsync(
            (staffUserId, token) => _orderService.CompleteByQrAsync(
                request.Token,
                staffUserId,
                token),
            cancellationToken);

    private async Task<ActionResult<OrderResponse>> ProcessAsync(
        Func<Guid, CancellationToken, Task<OrderMutationResult>> operation,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not Guid staffUserId)
        {
            return Unauthorized();
        }

        var result = await operation(
            staffUserId,
            cancellationToken);

        if (result.Status == OrderMutationStatus.Succeeded
            && result.Order is not null)
        {
            return Ok(result.Order);
        }

        return result.Status switch
        {
            OrderMutationStatus.NotFound =>
                NotFound(Problem("Sipariş bulunamadı.")),
            OrderMutationStatus.InvalidRowVersion =>
                BadRequest(Problem("RowVersion geçersiz.")),
            OrderMutationStatus.InvalidQrCode =>
                BadRequest(Problem("QR kod geçersiz veya süresi dolmuş.")),
            OrderMutationStatus.InvalidTransition =>
                Conflict(Problem("Sipariş bu işleme uygun durumda değil.")),
            OrderMutationStatus.ConcurrencyConflict =>
                Conflict(Problem("Sipariş başka bir kullanıcı tarafından değiştirildi.")),
            _ => StatusCode(
                StatusCodes.Status500InternalServerError)
        };
    }

    private static ProblemDetails Problem(string detail) =>
        new() { Detail = detail };
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

## `src/SecureShop.Api/appsettings.json`

Uzantı: `.json`

``json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=SecureShopDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True",
    "mssql.newEditorConnectionBehavior": "none"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "QrCodes": {
    "Orders": {
      "VerificationBaseUrl": "https://localhost:7002/employee/orders/verify",
      "LifetimeMinutes": 43200
    }
  },
  "AllowedHosts": "*"
}
``

## `src/SecureShop.Mvc/Security/AppRoles.cs`

Uzantı: `.cs`

``csharp
namespace SecureShop.Mvc.Security;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Employee = "Employee";
    public const string Customer = "Kunde";
}
``

## `src/SecureShop.Mvc/Models/Requests/ProcessOrderRequest.cs`

Uzantı: `.cs`

``csharp
namespace SecureShop.Mvc.Models.Requests;

public sealed record ProcessOrderRequest(string RowVersion);
``

## `src/SecureShop.Mvc/Models/Requests/VerifyOrderQrRequest.cs`

Uzantı: `.cs`

``csharp
namespace SecureShop.Mvc.Models.Requests;

public sealed record VerifyOrderQrRequest(string Token);
``

## `src/SecureShop.Mvc/Models/ViewModels/QrVerificationViewModel.cs`

Uzantı: `.cs`

``csharp
using System.ComponentModel.DataAnnotations;
using SecureShop.Mvc.Models.Responses;

namespace SecureShop.Mvc.Models.ViewModels;

public sealed class QrVerificationViewModel
{
    [Required]
    [Display(Name = "QR doğrulama token'ı")]
    public string Token { get; set; } = string.Empty;

    public OrderResponse? Order { get; set; }
}
``

## `src/SecureShop.Mvc/Services/Interfaces/IOrderApiService.cs`

Uzantı: `.cs`

``csharp
using SecureShop.Mvc.Http;
using SecureShop.Mvc.Models.Requests;
using SecureShop.Mvc.Models.Responses;

namespace SecureShop.Mvc.Services.Interfaces;

public interface IOrderApiService
{
    Task<ApiResponse<OrderResponse>> CreateAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<IReadOnlyList<OrderResponse>>> GetMineAsync(
        CancellationToken cancellationToken = default);

    Task<ApiResponse<OrderResponse>> GetMineAsync(
        string orderNumber,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<IReadOnlyList<OrderResponse>>> GetStaffAsync(
        CancellationToken cancellationToken = default);

    Task<ApiResponse<OrderResponse>> GetStaffAsync(
        string orderNumber,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<OrderResponse>> ApproveAsync(
        string orderNumber,
        ProcessOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<OrderResponse>> MarkReadyAsync(
        string orderNumber,
        ProcessOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<OrderResponse>> CancelAsync(
        string orderNumber,
        ProcessOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<OrderResponse>> VerifyQrAsync(
        VerifyOrderQrRequest request,
        CancellationToken cancellationToken = default);
}
``

## `src/SecureShop.Mvc/Services/Api/OrderApiService.cs`

Uzantı: `.cs`

``csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SecureShop.Mvc.Http;
using SecureShop.Mvc.Models.Requests;
using SecureShop.Mvc.Models.Responses;
using SecureShop.Mvc.Services.Interfaces;

namespace SecureShop.Mvc.Services.Api;

public sealed class OrderApiService : IOrderApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OrderApiService> _logger;

    public OrderApiService(
        HttpClient httpClient,
        ILogger<OrderApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<ApiResponse<OrderResponse>> CreateAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<OrderResponse>(
            new HttpRequestMessage(HttpMethod.Post, "api/orders")
            {
                Content = JsonContent.Create(request)
            },
            cancellationToken);

    public Task<ApiResponse<IReadOnlyList<OrderResponse>>> GetMineAsync(
        CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<OrderResponse>>(
            new HttpRequestMessage(HttpMethod.Get, "api/orders"),
            cancellationToken);

    public Task<ApiResponse<OrderResponse>> GetMineAsync(
        string orderNumber,
        CancellationToken cancellationToken = default) =>
        SendAsync<OrderResponse>(
            new HttpRequestMessage(
                HttpMethod.Get,
                $"api/orders/{Encode(orderNumber)}"),
            cancellationToken);

    public Task<ApiResponse<IReadOnlyList<OrderResponse>>> GetStaffAsync(
        CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<OrderResponse>>(
            new HttpRequestMessage(
                HttpMethod.Get,
                "api/employee/orders"),
            cancellationToken);

    public Task<ApiResponse<OrderResponse>> GetStaffAsync(
        string orderNumber,
        CancellationToken cancellationToken = default) =>
        SendAsync<OrderResponse>(
            new HttpRequestMessage(
                HttpMethod.Get,
                $"api/employee/orders/{Encode(orderNumber)}"),
            cancellationToken);

    public Task<ApiResponse<OrderResponse>> ApproveAsync(
        string orderNumber,
        ProcessOrderRequest request,
        CancellationToken cancellationToken = default) =>
        ProcessAsync(
            orderNumber,
            "approve",
            request,
            cancellationToken);

    public Task<ApiResponse<OrderResponse>> MarkReadyAsync(
        string orderNumber,
        ProcessOrderRequest request,
        CancellationToken cancellationToken = default) =>
        ProcessAsync(
            orderNumber,
            "ready",
            request,
            cancellationToken);

    public Task<ApiResponse<OrderResponse>> CancelAsync(
        string orderNumber,
        ProcessOrderRequest request,
        CancellationToken cancellationToken = default) =>
        ProcessAsync(
            orderNumber,
            "cancel",
            request,
            cancellationToken);

    public Task<ApiResponse<OrderResponse>> VerifyQrAsync(
        VerifyOrderQrRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<OrderResponse>(
            new HttpRequestMessage(
                HttpMethod.Post,
                "api/employee/orders/verify-qr")
            {
                Content = JsonContent.Create(request)
            },
            cancellationToken);

    private Task<ApiResponse<OrderResponse>> ProcessAsync(
        string orderNumber,
        string operation,
        ProcessOrderRequest request,
        CancellationToken cancellationToken) =>
        SendAsync<OrderResponse>(
            new HttpRequestMessage(
                HttpMethod.Post,
                $"api/employee/orders/{Encode(orderNumber)}/{operation}")
            {
                Content = JsonContent.Create(request)
            },
            cancellationToken);

    private async Task<ApiResponse<T>> SendAsync<T>(
        HttpRequestMessage request,
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
                    return ApiResponse<T>.Failure(
                        response.StatusCode,
                        await ReadErrorAsync(
                            response,
                            cancellationToken));
                }

                var data = await response.Content
                    .ReadFromJsonAsync<T>(
                        cancellationToken: cancellationToken);

                return data is null
                    ? ApiResponse<T>.Failure(
                        HttpStatusCode.BadGateway,
                        "API geçerli bir sipariş response'u döndürmedi.")
                    : ApiResponse<T>.Success(
                        response.StatusCode,
                        data);
            }
            catch (HttpRequestException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Sipariş API isteği tamamlanamadı.");

                return ApiResponse<T>.Failure(
                    HttpStatusCode.ServiceUnavailable,
                    "SecureShop API hizmetine ulaşılamıyor.");
            }
            catch (JsonException exception)
            {
                _logger.LogError(
                    exception,
                    "Sipariş API response'u okunamadı.");

                return ApiResponse<T>.Failure(
                    HttpStatusCode.BadGateway,
                    "API sipariş response formatı geçersiz.");
            }
        }
    }

    private static async Task<string> ReadErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var problem = await response.Content
                .ReadFromJsonAsync<ProblemDetails>(
                    cancellationToken: cancellationToken);

            if (!string.IsNullOrWhiteSpace(problem?.Detail))
            {
                return problem.Detail;
            }
        }
        catch (JsonException)
        {
            return "Sipariş işlemi tamamlanamadı.";
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized =>
                "Sipariş işlemi için giriş yapmalısınız.",
            HttpStatusCode.Forbidden =>
                "Bu sipariş işlemi için yetkiniz yok.",
            HttpStatusCode.NotFound =>
                "Sipariş bulunamadı.",
            HttpStatusCode.Conflict =>
                "Sipariş durumu değişti. Sayfayı yenileyin.",
            _ => "Sipariş işlemi tamamlanamadı."
        };
    }

    private static string Encode(string value) =>
        Uri.EscapeDataString(value.Trim());
}
``

## `src/SecureShop.Mvc/Controllers/EmployeeOrdersController.cs`

Uzantı: `.cs`

``csharp
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
``

## `src/SecureShop.Mvc/Views/EmployeeOrders/Index.cshtml`

Uzantı: `.cshtml`

``cshtml
@model SecureShop.Mvc.Models.ViewModels.OrderListViewModel
@{
    ViewData["Title"] = "Sipariş yönetimi";
}

<div class="d-flex justify-content-between align-items-center mb-4">
    <div>
        <span class="text-uppercase text-primary fw-semibold small">Personel</span>
        <h1 class="mb-0">Sipariş yönetimi</h1>
    </div>
    <a asp-action="Verify" class="btn btn-primary">QR doğrula</a>
</div>

@if (!string.IsNullOrWhiteSpace(Model.ErrorMessage))
{
    <div class="alert alert-danger">@Model.ErrorMessage</div>
}
else
{
    <div class="card border-0 shadow-sm">
        <div class="table-responsive">
            <table class="table align-middle mb-0">
                <thead>
                    <tr>
                        <th>Sipariş</th>
                        <th>Tarih</th>
                        <th>Alıcı</th>
                        <th>Toplam</th>
                        <th>Durum</th>
                        <th></th>
                    </tr>
                </thead>
                <tbody>
                    @foreach (var order in Model.Orders)
                    {
                        <tr>
                            <td class="fw-semibold">@order.OrderNumber</td>
                            <td>@order.CreatedAtUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm")</td>
                            <td>@order.RecipientName</td>
                            <td>@order.TotalAmount.ToString("N2") €</td>
                            <td><span class="badge text-bg-primary">@order.Status</span></td>
                            <td class="text-end">
                                <a asp-action="Details"
                                   asp-route-orderNumber="@order.OrderNumber"
                                   class="btn btn-sm btn-outline-primary">
                                    İşlemler
                                </a>
                            </td>
                        </tr>
                    }
                </tbody>
            </table>
        </div>
    </div>
}
``

## `src/SecureShop.Mvc/Views/EmployeeOrders/Details.cshtml`

Uzantı: `.cshtml`

``cshtml
@model SecureShop.Mvc.Models.Responses.OrderResponse
@{
    ViewData["Title"] = $"Sipariş {Model.OrderNumber}";
}

@if (TempData["SuccessMessage"] is string successMessage)
{
    <div class="alert alert-success">@successMessage</div>
}
@if (TempData["ErrorMessage"] is string errorMessage)
{
    <div class="alert alert-danger">@errorMessage</div>
}

<div class="d-flex flex-wrap justify-content-between gap-3 mb-4">
    <div>
        <span class="text-uppercase text-primary fw-semibold small">Personel sipariş işlemi</span>
        <h1 class="mt-2 mb-1">@Model.OrderNumber</h1>
        <span class="badge text-bg-primary">@Model.Status</span>
    </div>
    <a asp-action="Index" class="btn btn-outline-secondary">Listeye dön</a>
</div>

<div class="card border-0 shadow-sm mb-4">
    <div class="card-body p-4">
        <div class="row g-4">
            <div class="col-md-7">
                <h2 class="h5">Ürünler</h2>
                @foreach (var item in Model.Items)
                {
                    <div class="d-flex justify-content-between py-2 border-bottom">
                        <span>@item.ProductName × @item.Quantity</span>
                        <strong>@item.LineTotal.ToString("N2") €</strong>
                    </div>
                }
                <div class="d-flex justify-content-between fs-5 pt-3">
                    <strong>Toplam</strong>
                    <strong>@Model.TotalAmount.ToString("N2") €</strong>
                </div>
            </div>
            <div class="col-md-5">
                <h2 class="h5">Teslimat</h2>
                <strong>@Model.RecipientName</strong>
                <div>@Model.AddressLine</div>
                <div>@Model.PostalCode @Model.City</div>
                <div>@Model.Country</div>
            </div>
        </div>
    </div>
</div>

<div class="d-flex flex-wrap gap-2">
    @if (Model.Status == "PendingApproval")
    {
        <form asp-action="Approve" asp-route-orderNumber="@Model.OrderNumber" method="post">
            <input type="hidden" name="rowVersion" value="@Model.RowVersion" />
            <button class="btn btn-success" type="submit">Siparişi onayla</button>
        </form>
        <form asp-action="Cancel" asp-route-orderNumber="@Model.OrderNumber" method="post">
            <input type="hidden" name="rowVersion" value="@Model.RowVersion" />
            <button class="btn btn-outline-danger" type="submit">Reddet ve stoğu iade et</button>
        </form>
    }
    else if (Model.Status == "Approved")
    {
        <form asp-action="MarkReady" asp-route-orderNumber="@Model.OrderNumber" method="post">
            <input type="hidden" name="rowVersion" value="@Model.RowVersion" />
            <button class="btn btn-primary" type="submit">Teslime hazır yap</button>
        </form>
        <form asp-action="Cancel" asp-route-orderNumber="@Model.OrderNumber" method="post">
            <input type="hidden" name="rowVersion" value="@Model.RowVersion" />
            <button class="btn btn-outline-danger" type="submit">İptal et ve stoğu iade et</button>
        </form>
    }
    else if (Model.Status == "ReadyForPickup")
    {
        <a asp-action="Verify" class="btn btn-primary">Teslim QR kodunu doğrula</a>
    }
</div>
``

## `src/SecureShop.Mvc/Views/EmployeeOrders/Verify.cshtml`

Uzantı: `.cshtml`

``cshtml
@model SecureShop.Mvc.Models.ViewModels.QrVerificationViewModel
@{
    ViewData["Title"] = "Sipariş QR doğrulama";
}

<div class="row justify-content-center">
    <div class="col-lg-7">
        <div class="card border-0 shadow-sm">
            <div class="card-body p-4 p-lg-5">
                <span class="text-uppercase text-primary fw-semibold small">Güvenli teslim</span>
                <h1 class="mt-2">QR kod doğrula</h1>
                <p class="text-body-secondary">
                    QR bağlantısından gelen token otomatik dolar. Gerekirse token'ı elle yapıştırın.
                </p>
                @if (TempData["SuccessMessage"] is string successMessage)
                {
                    <div class="alert alert-success">@successMessage</div>
                }
                <form asp-action="Verify" method="post">
                    <div asp-validation-summary="ModelOnly" class="alert alert-danger"></div>
                    <label asp-for="Token" class="form-label"></label>
                    <textarea asp-for="Token" class="form-control" rows="5"></textarea>
                    <span asp-validation-for="Token" class="text-danger"></span>
                    <button class="btn btn-primary mt-3" type="submit">
                        Doğrula ve teslimi tamamla
                    </button>
                </form>
                @if (Model.Order is not null)
                {
                    <div class="alert alert-success mt-4 mb-0">
                        <strong>@Model.Order.OrderNumber</strong> teslim edildi.
                    </div>
                }
            </div>
        </div>
    </div>
</div>

@section Scripts {
    <partial name="_ValidationScriptsPartial" />
}
``

## `src/SecureShop.Mvc/Views/Orders/Details.cshtml`

Uzantı: `.cshtml`

``cshtml
@model SecureShop.Mvc.Models.Responses.OrderResponse
@{
    ViewData["Title"] = $"Sipariş {Model.OrderNumber}";
}

@if (TempData["SuccessMessage"] is string successMessage)
{
    <div class="alert alert-success">@successMessage</div>
}

<div class="d-flex flex-wrap justify-content-between align-items-start gap-3 mb-4">
    <div>
        <span class="text-uppercase text-primary fw-semibold small">Sipariş detayı</span>
        <h1 class="mt-2 mb-1">@Model.OrderNumber</h1>
        <span class="badge text-bg-primary">@Model.Status</span>
    </div>
    <a asp-action="Index" class="btn btn-outline-secondary">Siparişlerime dön</a>
</div>

<div class="row g-4">
    <div class="col-lg-8">
        <div class="card border-0 shadow-sm">
            <div class="card-body p-4">
                <h2 class="h5 mb-3">Ürünler</h2>
                @foreach (var item in Model.Items)
                {
                    <div class="d-flex justify-content-between gap-3 py-3 border-bottom">
                        <div>
                            <a asp-controller="Products" asp-action="Details"
                               asp-route-sku="@item.Sku"
                               class="fw-semibold text-decoration-none">
                                @item.ProductName
                            </a>
                            <div class="text-body-secondary small">
                                SKU: @item.Sku · @item.Quantity adet × @item.UnitPrice.ToString("N2") €
                            </div>
                        </div>
                        <strong>@item.LineTotal.ToString("N2") €</strong>
                    </div>
                }
                <div class="d-flex justify-content-between fs-5 pt-3">
                    <strong>Toplam</strong>
                    <strong>@Model.TotalAmount.ToString("N2") €</strong>
                </div>
            </div>
        </div>
    </div>
    <div class="col-lg-4">
        <div class="card border-0 shadow-sm mb-4">
            <div class="card-body p-4">
                <h2 class="h5">Teslimat</h2>
                <strong>@Model.RecipientName</strong>
                <div>@Model.AddressLine</div>
                <div>@Model.PostalCode @Model.City</div>
                <div>@Model.Country</div>
            </div>
        </div>
        @if (!string.IsNullOrWhiteSpace(Model.QrCodeDataUrl))
        {
            <div class="card border-0 shadow-sm text-center">
                <div class="card-body p-4">
                    <h2 class="h5">Teslim QR kodu</h2>
                    <p class="small text-body-secondary">
                        Siparişi teslim alırken personele gösterin.
                    </p>
                    <img src="@Model.QrCodeDataUrl"
                         alt="Sipariş teslim QR kodu"
                         class="order-qr-image" />
                </div>
            </div>
        }
    </div>
</div>
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

# 7. Kod Açıklaması

`Order` entity'si izinli durum geçişlerini kendi içinde zorunlu kılar. Personel mutasyonlarında istemciden gelen base64 `RowVersion` EF Core original value olarak atanır. QR token, time-limited Data Protection purpose string'iyle korunur; değiştirilen veya süresi dolan token reddedilir. Token yalnızca `ReadyForPickup` siparişini `Completed` yapabilir. API personel kimliğini cookie claim'inden alır.

# 8. API–MVC Veri Akışı

```text
Employee Razor View
    ↓
MVC EmployeeOrdersController
    ↓
IOrderApiService
    ↓
/api/employee/orders/* (StaffOnly)
    ↓
OrderService durum makinesi
    ↓
Data Protection token + QRCoder PNG
    ↓
SQL Server Order + RowVersion
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

Personel bekleyen siparişi onaylayabilir, teslime hazır yapabilir veya izinli aşamada iptal edebilir. Onaylı/teslime hazır siparişin müşteri detayında QR görünür. Geçerli QR yalnızca personel oturumunda teslimi tamamlar; oynanmış veya süresi dolmuş token reddedilir.

# 12. Yaygın Hatalar

- Eski `RowVersion` ile işlem `409 Conflict` verir.
- Yanlış durum geçişi `409` verir.
- QR ayarındaki doğrulama URL'si HTTPS değilse uygulama başlangıç doğrulaması başarısız olur.
- QR token loglara veya audit detaylarına yazılmaz.

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