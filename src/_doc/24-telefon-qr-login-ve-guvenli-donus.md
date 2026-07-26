# 1. Bu Adımın Amacı

Müşterinin sipariş detayında gösterilen QR kodunun telefonun standart kamera
uygulamasıyla taranmasını, telefonda SecureShop MVC uygulamasının açılmasını,
oturum yoksa Employee/Admin login ekranına yönlendirilmesini ve başarılı girişten
sonra token kaybolmadan güvenli teslim onay ekranına dönülmesini sağlamak.

# 2. Etkilenen Uygulama

```text
SecureShop.Api
SecureShop.Mvc
Public HTTPS / Development Tunnel
```

SQL Server şeması değişmediği için migration gerekmez.

# 3. Bu Adımda Yapılacaklar

1. QR içine telefondan erişilebilir public HTTPS MVC adresi koymak.
2. QR token ömrünü 15 dakika yapmak.
3. Yetkisiz QR isteğini cookie challenge ile login sayfasına göndermek.
4. Yerel returnUrl değerini login formunda korumak.
5. Başarılı login sonrasında QR ekranına dönmek.
6. Harici returnUrl değerlerini reddederek open redirect önlemek.
7. Yanlış rolle girişte güvenli hesap değiştirme seçeneği göstermek.
8. GET isteğinde teslim mutasyonu yapmamak.
9. Token'ı görünür textarea yerine hidden input içinde taşımak.
10. Teslimi yalnızca anti-forgery korumalı POST ile tamamlamak.

# 4. CLI Komutları

Çalışma dizini: `D:\Code\ASP.NET\SecureShop`

```powershell
dotnet restore SecureShop.sln
dotnet build SecureShop.sln -c Release --no-restore
dotnet test SecureShop.sln -c Release --no-build
```

Telefon testi için Microsoft Dev Tunnels kurulumu:

```powershell
winget install Microsoft.devtunnel
devtunnel user login
devtunnel host -p 7002 --protocol https --allow-anonymous
```

`--allow-anonymous` yalnızca tunnel katmanındaki Microsoft hesabı zorunluluğunu
kaldırır. SecureShop Employee/Admin login ve authorization kontrolleri korunur.

Komutun döndürdüğü HTTPS adresini `.env` dosyasına yaz:

```dotenv
QrCodes__Orders__VerificationBaseUrl=https://TUNNEL-HTTPS-ADRESI/employee/orders/verify
QrCodes__Orders__LifetimeMinutes=15
```

QR URL API tarafından üretildiği için `.env` değişikliğinden sonra API yeniden
başlatılmalıdır.

# 5. Güncel Proje Yapısı

```text
SecureShop/
├── .env.example
├── src/SecureShop.Api/
│   ├── Features/QrCodes/
│   ├── Features/Orders/OrderService.cs
│   ├── Program.cs
│   └── appsettings.json
├── src/SecureShop.Mvc/
│   ├── Controllers/AccountController.cs
│   ├── Models/ViewModels/LoginViewModel.cs
│   └── Views/
│       ├── Account/{Login,Forbidden}.cshtml
│       └── EmployeeOrders/Verify.cshtml
└── tests/SecureShop.Mvc.Tests/AccountControllerTests.cs
```

# 6. Dosya Bazında Eksiksiz Kodlar

## `.env.example`

Uzantı: `.example`

```dotenv
# SecureShop local secrets (copy to .env and replace placeholders)
Authentication__Google__ClientId=replace-with-google-client-id
Authentication__Google__ClientSecret=replace-with-google-client-secret

SeedUsers__Admin__Email=admin@secureshop.local
SeedUsers__Admin__Password=replace-with-strong-admin-password
SeedUsers__Admin__FirstName=SecureShop
SeedUsers__Admin__LastName=Admin

SeedUsers__Employee__Email=employee@secureshop.local
SeedUsers__Employee__Password=replace-with-strong-employee-password
SeedUsers__Employee__FirstName=SecureShop
SeedUsers__Employee__LastName=Employee

SeedUsers__Customer__Email=customer@secureshop.local
SeedUsers__Customer__Password=replace-with-strong-customer-password
SeedUsers__Customer__FirstName=SecureShop
SeedUsers__Customer__LastName=Customer

# Phone QR scanning requires a real/public HTTPS MVC address.
# Example: https://your-tunnel-7002.devtunnels.ms/employee/orders/verify
QrCodes__Orders__VerificationBaseUrl=https://replace-with-public-https-host/employee/orders/verify
QrCodes__Orders__LifetimeMinutes=15
```

## `src/SecureShop.Api/Features/QrCodes/OrderQrOptions.cs`

Uzantı: `.cs`

```csharp
namespace SecureShop.Api.Features.QrCodes;

public sealed class OrderQrOptions
{
    public const string SectionName = "QrCodes:Orders";

    public string VerificationBaseUrl { get; set; } =
        "https://localhost:7002/employee/orders/verify";

    public int LifetimeMinutes { get; set; } = 15;
}
```

## `src/SecureShop.Api/Features/QrCodes/OrderQrTokenService.cs`

Uzantı: `.cs`

```csharp
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
```

## `src/SecureShop.Api/Features/Orders/OrderService.cs`

Uzantı: `.cs`

```csharp
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
```

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

if (!builder.Environment.IsEnvironment("Testing"))
{
    DotEnvConfiguration.AddMissingFromDotEnv(
        builder.Configuration,
        builder.Environment.ContentRootPath);
}

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
    .Validate(
        options =>
            builder.Environment.IsDevelopment()
            || !Uri.TryCreate(
                options.VerificationBaseUrl,
                UriKind.Absolute,
                out var uri)
            || !uri.IsLoopback,
        "Production ortamında QR doğrulama adresi localhost olamaz.")
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

```json
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
      "LifetimeMinutes": 15
    }
  },
  "AllowedHosts": "*"
}
```

## `src/SecureShop.Mvc/Models/ViewModels/LoginViewModel.cs`

Uzantı: `.cs`

```csharp
using System.ComponentModel.DataAnnotations;
namespace SecureShop.Mvc.Models.ViewModels;
public sealed class LoginViewModel
{
    [Required, EmailAddress, Display(Name="E-posta")] public string Email { get; set; }=string.Empty;
    [Required, DataType(DataType.Password), Display(Name="Parola")] public string Password { get; set; }=string.Empty;

    [StringLength(2048)]
    public string? ReturnUrl { get; set; }
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
    public IActionResult Login([FromQuery] string? returnUrl)
    {
        var safeReturnUrl = GetSafeReturnUrl(returnUrl);

        if (User.Identity?.IsAuthenticated == true)
        {
            return safeReturnUrl is null
                ? RedirectToAction(nameof(Session))
                : LocalRedirect(safeReturnUrl);
        }

        return View(new LoginViewModel
        {
            ReturnUrl = safeReturnUrl
        });
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

        var safeReturnUrl = GetSafeReturnUrl(model.ReturnUrl);

        return safeReturnUrl is null
            ? RedirectToAction(nameof(Session))
            : LocalRedirect(safeReturnUrl);
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

    [AllowAnonymous]
    [HttpGet("forbidden")]
    public IActionResult Forbidden([FromQuery] string? returnUrl)
    {
        ViewData["ReturnUrl"] = GetSafeReturnUrl(returnUrl);
        return View();
    }

    [Authorize]
    [HttpPost("switch-account")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SwitchAccount(
        string? returnUrl)
    {
        await HttpContext.SignOutAsync(
            SharedCookieAuthenticationDefaults.AuthenticationScheme);

        return RedirectToAction(
            nameof(Login),
            new
            {
                returnUrl = GetSafeReturnUrl(returnUrl)
            });
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

    private string? GetSafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl)
        && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : null;
}
```

## `src/SecureShop.Mvc/Views/Account/Login.cshtml`

Uzantı: `.cshtml`

```cshtml
@model SecureShop.Mvc.Models.ViewModels.LoginViewModel
@{
    ViewData["Title"] = "Giriş";
    var continuesToProtectedPage =
        !string.IsNullOrWhiteSpace(Model.ReturnUrl);
}

<div class="row justify-content-center">
    <div class="col-lg-5 col-md-7">
        <div class="card border-0 shadow-sm login-card">
            <div class="card-body p-4 p-lg-5">
                <span class="text-uppercase text-primary fw-semibold small">
                    Güvenli oturum
                </span>
                <h1 class="h2 mt-2">Giriş yap</h1>
                @if (continuesToProtectedPage)
                {
                    <div class="alert alert-info mt-3" role="status">
                        QR doğrulamasına devam etmek için Employee veya Admin
                        hesabıyla giriş yapın.
                    </div>
                }
                <form asp-action="Login" method="post" class="mt-4">
                    <div asp-validation-summary="ModelOnly" class="alert alert-danger"></div>
                    <input type="hidden"
                           name="ReturnUrl"
                           value="@Model.ReturnUrl" />
                    <div class="mb-3">
                        <label asp-for="Email" class="form-label"></label>
                        <input asp-for="Email"
                               class="form-control form-control-lg"
                               autocomplete="username"
                               autofocus />
                        <span asp-validation-for="Email" class="text-danger"></span>
                    </div>
                    <div class="mb-4">
                        <label asp-for="Password" class="form-label"></label>
                        <input asp-for="Password"
                               class="form-control form-control-lg"
                               autocomplete="current-password" />
                        <span asp-validation-for="Password" class="text-danger"></span>
                    </div>
                    <button class="btn btn-primary btn-lg w-100" type="submit">
                        @(continuesToProtectedPage
                            ? "Giriş yap ve QR doğrulamaya devam et"
                            : "Giriş yap")
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

## `src/SecureShop.Mvc/Views/Account/Forbidden.cshtml`

Uzantı: `.cshtml`

```cshtml
@{
    ViewData["Title"] = "Erişim reddedildi";
    var returnUrl = ViewData["ReturnUrl"] as string;
}

<section class="card border-0 shadow-sm mx-auto" style="max-width: 42rem;">
    <div class="card-body p-5 text-center">
        <div class="display-5 mb-3" aria-hidden="true">🔒</div>
        <h1 class="h3">Bu sayfaya erişim yetkiniz yok</h1>
        <p class="text-body-secondary">
            Bu işlem hesabınızdaki role açık değil. QR teslim doğrulaması için
            Employee veya Admin hesabı gerekir.
        </p>
        <div class="d-flex flex-wrap justify-content-center gap-2">
            <a asp-controller="Home"
               asp-action="Index"
               class="btn btn-outline-secondary">
                Ana sayfaya dön
            </a>
            @if (User.Identity?.IsAuthenticated == true)
            {
                <form asp-controller="Account"
                      asp-action="SwitchAccount"
                      method="post">
                    <input type="hidden"
                           name="returnUrl"
                           value="@returnUrl" />
                    <button class="btn btn-primary" type="submit">
                        Farklı hesapla giriş yap
                    </button>
                </form>
            }
        </div>
    </div>
</section>
```

## `src/SecureShop.Mvc/Views/EmployeeOrders/Verify.cshtml`

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
                    <div class="alert alert-warning mb-0">
                        QR token bulunamadı. Müşterinin sipariş detayındaki QR
                        kodunu telefon kamerasıyla yeniden tarayın.
                    </div>
                }
            </div>
        </div>
    </div>
</div>

@section Scripts {
    <partial name="_ValidationScriptsPartial" />
}
```

## `tests/SecureShop.Mvc.Tests/AccountControllerTests.cs`

Uzantı: `.cs`

```csharp
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using SecureShop.Mvc.Controllers;
using SecureShop.Mvc.Http;
using SecureShop.Mvc.Models.Responses;
using SecureShop.Mvc.Models.ViewModels;
using SecureShop.Mvc.Services.Interfaces;

namespace SecureShop.Mvc.Tests;

public sealed class AccountControllerTests
{
    private const string QrReturnUrl =
        "/employee/orders/verify?token=protected-token";

    [Fact]
    public void LoginGet_PreservesLocalQrReturnUrl()
    {
        var controller = CreateController();

        var result = Assert.IsType<ViewResult>(
            controller.Login(QrReturnUrl));
        var model = Assert.IsType<LoginViewModel>(
            result.Model);

        Assert.Equal(QrReturnUrl, model.ReturnUrl);
    }

    [Fact]
    public void LoginGet_RejectsExternalReturnUrl()
    {
        var controller = CreateController();

        var result = Assert.IsType<ViewResult>(
            controller.Login(
                "https://attacker.example/steal-token"));
        var model = Assert.IsType<LoginViewModel>(
            result.Model);

        Assert.Null(model.ReturnUrl);
    }

    [Fact]
    public async Task LoginPost_RedirectsBackToLocalQrPage()
    {
        var controller = CreateController();
        var model = new LoginViewModel
        {
            Email = "employee@secureshop.local",
            Password = "test-password",
            ReturnUrl = QrReturnUrl
        };

        var result = await controller.Login(
            model,
            CancellationToken.None);

        var redirect = Assert.IsType<LocalRedirectResult>(
            result);
        Assert.Equal(QrReturnUrl, redirect.Url);
        Assert.True(controller.Response.Headers.ContainsKey(
            "Set-Cookie"));
    }

    [Fact]
    public async Task LoginPost_DoesNotRedirectToExternalUrl()
    {
        var controller = CreateController();
        var model = new LoginViewModel
        {
            Email = "employee@secureshop.local",
            Password = "test-password",
            ReturnUrl = "https://attacker.example/steal-token"
        };

        var result = await controller.Login(
            model,
            CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(
            result);
        Assert.Equal("Session", redirect.ActionName);
    }

    private static AccountController CreateController()
    {
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ControllerActionDescriptor());
        var controller = new AccountController(
            new SuccessfulAuthApiService())
        {
            ControllerContext = new ControllerContext(
                actionContext),
            Url = new UrlHelper(actionContext)
        };

        return controller;
    }

    private sealed class SuccessfulAuthApiService
        : IAuthApiService
    {
        public Task<LoginApiResult> LoginAsync(
            string email,
            string password,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new LoginApiResult(
                true,
                HttpStatusCode.OK,
                "__Host-SecureShop.Auth=test; path=/; secure; httponly",
                null));

        public Task<ApiResponse<AuthSessionResponse>> GetSessionAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
```

## `src/SecureShop.Api/Configuration/DotEnvConfiguration.cs`

Uzantı: `.cs`

``csharp
using Microsoft.Extensions.Configuration;

namespace SecureShop.Api.Configuration;

public static class DotEnvConfiguration
{
    private const string DotEnvFileName = ".env";

    public static void AddMissingFromDotEnv(
        ConfigurationManager configuration,
        string contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);

        var dotEnvPath = FindDotEnvPath(contentRootPath);

        if (dotEnvPath is null)
        {
            return;
        }

        var values = new Dictionary<string, string?>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var line in File.ReadLines(dotEnvPath))
        {
            if (!TryParseLine(line, out var key, out var value))
            {
                continue;
            }

            if (HasProcessEnvironmentValue(key))
            {
                continue;
            }

            values.TryAdd(key, value);
        }

        if (values.Count > 0)
        {
            configuration.AddInMemoryCollection(values);
        }
    }

    private static bool HasProcessEnvironmentValue(string key)
    {
        var environmentKey = key.Replace(
            ":",
            "__",
            StringComparison.Ordinal);

        return !string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable(
                    environmentKey))
            || !string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable(key));
    }

    private static string? FindDotEnvPath(
        string contentRootPath)
    {
        var startDirectories = new[]
        {
            contentRootPath,
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        };

        foreach (var startDirectory in startDirectories.Distinct())
        {
            var directory = new DirectoryInfo(startDirectory);

            while (directory is not null)
            {
                var candidate = Path.Combine(
                    directory.FullName,
                    DotEnvFileName);

                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }
        }

        return null;
    }

    private static bool TryParseLine(
        string line,
        out string key,
        out string value)
    {
        key = string.Empty;
        value = string.Empty;

        var trimmedLine = line.Trim();

        if (string.IsNullOrEmpty(trimmedLine)
            || trimmedLine.StartsWith('#'))
        {
            return false;
        }

        if (trimmedLine.StartsWith(
            "export ",
            StringComparison.OrdinalIgnoreCase))
        {
            trimmedLine = trimmedLine["export ".Length..].TrimStart();
        }

        var separatorIndex = trimmedLine.IndexOf('=');

        if (separatorIndex <= 0)
        {
            return false;
        }

        key = trimmedLine[..separatorIndex]
            .Trim()
            .Replace("__", ":", StringComparison.Ordinal);

        value = NormalizeValue(
            trimmedLine[(separatorIndex + 1)..].Trim());

        return !string.IsNullOrWhiteSpace(key);
    }

    private static string NormalizeValue(
        string value)
    {
        if (value.Length < 2)
        {
            return value;
        }

        var quote = value[0];

        if ((quote != '"' && quote != '\'')
            || value[^1] != quote)
        {
            return value;
        }

        var unquotedValue = value[1..^1];

        return quote == '"'
            ? unquotedValue
                .Replace("\\\"", "\"", StringComparison.Ordinal)
                .Replace("\\n", "\n", StringComparison.Ordinal)
                .Replace("\\r", "\r", StringComparison.Ordinal)
                .Replace("\\\\", "\\", StringComparison.Ordinal)
            : unquotedValue;
    }
}
``
## `tests/SecureShop.Api.UnitTests/DotEnvConfigurationTests.cs`

Uzantı: `.cs`

``csharp
using Microsoft.Extensions.Configuration;
using SecureShop.Api.Configuration;

namespace SecureShop.Api.UnitTests;

public sealed class DotEnvConfigurationTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"secureshop-dotenv-tests-{Guid.NewGuid():N}");

    [Fact]
    public void DotEnv_OverridesAppSettingsValue()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(
            Path.Combine(_directory, ".env"),
            "QrCodes__Orders__VerificationBaseUrl=https://phone.example/verify");
        var configuration = new ConfigurationManager();
        configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["QrCodes:Orders:VerificationBaseUrl"] =
                    "https://localhost/verify"
            });

        DotEnvConfiguration.AddMissingFromDotEnv(
            configuration,
            _directory);

        Assert.Equal(
            "https://phone.example/verify",
            configuration[
                "QrCodes:Orders:VerificationBaseUrl"]);
    }

    [Fact]
    public void ProcessEnvironment_OverridesDotEnvValue()
    {
        const string key =
            "SecureShopTest__PhoneQr__PublicUrl";
        const string configurationKey =
            "SecureShopTest:PhoneQr:PublicUrl";
        var originalValue =
            Environment.GetEnvironmentVariable(key);

        try
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(
                Path.Combine(_directory, ".env"),
                $"{key}=https://dotenv.example");
            Environment.SetEnvironmentVariable(
                key,
                "https://environment.example");
            var configuration = new ConfigurationManager();
            configuration.AddEnvironmentVariables();

            DotEnvConfiguration.AddMissingFromDotEnv(
                configuration,
                _directory);

            Assert.Equal(
                "https://environment.example",
                configuration[configurationKey]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                key,
                originalValue);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
``
## `tests/SecureShop.Api.IntegrationTests/SecureShopApiFactory.cs`

Uzantı: `.cs`

``csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SecureShop.Api.Data;
using SecureShop.Api.Domain.Entities;

namespace SecureShop.Api.IntegrationTests;

public sealed class SecureShopApiFactory
    : WebApplicationFactory<Program>
{
    private readonly string _keyRingPath = Path.Combine(
        Path.GetTempPath(),
        $"secureshop-api-tests-{Guid.NewGuid():N}");
    private readonly string _databaseName =
        $"SecureShop-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting(
            "Authentication:SharedCookie:KeyRingPath",
            _keyRingPath);
        builder.UseSetting(
            "Authentication:Google:ClientId",
            "integration-test-client");
        builder.UseSetting(
            "Authentication:Google:ClientSecret",
            "integration-test-secret");
        builder.UseSetting(
            "QrCodes:Orders:VerificationBaseUrl",
            "https://phone.secureshop.test/employee/orders/verify");
        builder.UseSetting(
            "QrCodes:Orders:LifetimeMinutes",
            "30");

        builder.ConfigureAppConfiguration(configuration =>
        {
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        "Server=(localdb)\\MSSQLLocalDB;Database=UnusedTestDb;Trusted_Connection=True",
                    ["Authentication:Google:ClientId"] =
                        "integration-test-client",
                    ["Authentication:Google:ClientSecret"] =
                        "integration-test-secret",
                    ["Authentication:SharedCookie:KeyRingPath"] =
                        _keyRingPath,
                    ["QrCodes:Orders:VerificationBaseUrl"] =
                        "https://phone.secureshop.test/employee/orders/verify",
                    ["QrCodes:Orders:LifetimeMinutes"] = "30"
                });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<
                IDbContextOptionsConfiguration<AppDbContext>>();

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(
                    _databaseName);
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var dbContext =
            scope.ServiceProvider.GetRequiredService<AppDbContext>();

        dbContext.Database.EnsureCreated();

        var category = new Category("Elektronik");
        var product = new Product(
            category.Id,
            "Integration Product",
            "INTEGRATION-SKU",
            29.90m,
            12,
            "Integration test product");

        dbContext.Categories.Add(category);
        dbContext.Products.Add(product);
        dbContext.SaveChanges();

        return host;
    }

    public HttpClient CreateHttpsClient() =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && Directory.Exists(_keyRingPath))
        {
            Directory.Delete(_keyRingPath, recursive: true);
        }
    }
}
``
# 7. Kod Açıklaması

Cookie authentication, `[Authorize]` ile korunan QR endpoint'ine anonim istek
geldiğinde `/Account/Login?ReturnUrl=...` adresine challenge üretir.
`AccountController`, yalnızca `Url.IsLocalUrl` kontrolünü geçen değeri modele
alır ve başarılı girişte `LocalRedirect` kullanır. Böylece token login boyunca
korunur ancak saldırganın harici alan adına yönlendirme yaptırması engellenir.

Razor hidden input güvenli model değerini açıkça kullanır. Ham GET ModelState
değerinin tag helper tarafından tekrar yazılması önlenir.

QR token, Time-Limited Data Protection ile korunur. Token değiştirilirse, süresi
dolarsa veya sipariş `ReadyForPickup` durumunda değilse API işlemi reddeder.
Teslim mutasyonu GET ile değil, anti-forgery korumalı POST ile yapılır.

# 8. API–MVC Veri Akışı

```text
Telefon kamerası QR taraması
    ↓
Public HTTPS /employee/orders/verify?token=...
    ↓
MVC cookie challenge
    ↓
/account/login?ReturnUrl=yerel-qr-adresi
    ↓
Employee/Admin e-posta + parola
    ↓
API Identity login + shared HttpOnly cookie
    ↓
LocalRedirect(ReturnUrl)
    ↓
QR teslim onay ekranı
    ↓
Anti-forgery korumalı POST
    ↓
API StaffOnly + süreli token doğrulaması
    ↓
Order → Completed
```

# 9. Uygulama Sırası

1. Public HTTPS MVC adresini/tunnel'ı hazırla.
2. `.env` QR doğrulama URL'sini güncelle.
3. API ve MVC'yi yeniden başlat.
4. Siparişi Employee ile `ReadyForPickup` durumuna getir.
5. Müşterinin sipariş detayındaki QR'ı telefondan tara.
6. Employee/Admin hesabıyla giriş yap.
7. Döndürülen ekranda teslimi onayla.

# 10. Çalıştırma ve Test

Terminal 1 — API:

```powershell
cd src\SecureShop.Api
dotnet watch run --launch-profile https
```

Terminal 2 — MVC:

```powershell
cd src\SecureShop.Mvc
dotnet watch run --launch-profile https
```

Terminal 3 — public tunnel:

```powershell
devtunnel host -p 7002 --protocol https --allow-anonymous
```

Telefon Wi-Fi veya mobil veri üzerinden tunnel HTTPS adresini açabilmelidir.

# 11. Beklenen Sonuç

```text
QR tarandı
→ SecureShop telefonda açıldı
→ Oturum yoksa login ekranı açıldı
→ Employee/Admin giriş yaptı
→ Aynı QR token ile doğrulama ekranına dönüldü
→ Kullanıcı teslim onayına bastı
→ Sipariş Completed oldu
```

Doğrulanan otomatik/canlı sonuç:

```text
Anonim QR challenge: 302
ReturnUrl ve token login boyunca korundu
Employee login: 302
QR doğrulama ekranı: 200
Harici returnUrl hidden alana yansıtılmadı
Open redirect reddedildi
```

# 12. Yaygın Hatalar

- QR içinde `localhost` varsa telefon kendi localhost'una bağlanmaya çalışır.
- LAN IP kullanıldığında geliştirme sertifikası telefonda güvenilir olmayabilir.
- Tunnel URL her yeniden oluşturmada değişirse `.env` ve API yeniden başlatılmalıdır.
- Telefon tunnel'a ulaşamıyorsa `--allow-anonymous` seçeneğini ve tunnel sürecini kontrol et.
- Customer hesabı QR teslimi yapamaz; Employee veya Admin gerekir.
- Süresi dolan QR için müşteri sipariş detayını yenileyerek yeni QR üretmelidir.
- GET isteği teslimi tamamlamaz; onay düğmesine basılması gerekir.

# 13. Tamamlama Kontrol Listesi

```text
[x] QR içeriği HTTPS URL taşıyor
[x] QR token süreli ve kurcalamaya dayanıklı
[x] Token ömrü 15 dakika
[x] Anonim QR isteği login sayfasına gidiyor
[x] Login sonrasında QR ekranına geri dönülüyor
[x] Harici returnUrl reddediliyor
[x] Yanlış rol için hesap değiştirme seçeneği var
[x] GET isteği veri değiştirmiyor
[x] Teslim POST ve CSRF ile korunuyor
[x] Secret içermeyen .env.example eklendi
[x] Otomatik testler geçti
```

## Kullanılan Microsoft kaynakları

- https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-enable-qrcodes?view=aspnetcore-10.0
- https://learn.microsoft.com/de-de/windows/mixed-reality/develop/native/qr-code-tracking-cs-cpp
- https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/get-started
- https://learn.microsoft.com/en-us/aspnet/core/test/dev-tunnels?view=aspnetcore-10.0