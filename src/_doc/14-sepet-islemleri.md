# Sepet İşlemleri

# 1. Bu Adımın Amacı

Bu adımda giriş yapmış müşteri için kalıcı ve kullanıcıya özel alışveriş sepeti oluşturuldu. Sepete ürün ekleme, miktar güncelleme, ürün çıkarma, sepeti temizleme ve sepeti görüntüleme işlemleri tamamlandı.

Kullanıcı kimliği, fiyat ve stok MVC'den alınmaz. API kullanıcıyı imzalı authentication cookie içindeki `NameIdentifier` claim'iyle belirler; güncel fiyatı ve stoğu SQL Server'dan okur.

# 2. Etkilenen Uygulama

```text
SecureShop.Api : Domain modeli, EF Core, iş kuralları, authorization ve endpointler
SecureShop.Mvc : Typed HttpClient, Razor formları, sepet ekranı ve navbar
SQL Server     : Carts ve CartItems tabloları
```

# 3. Bu Adımda Yapılanlar

- Her kullanıcı için en fazla bir sepet olacak şekilde `Cart` modeli oluşturuldu.
- Aynı ürünün aynı sepette tek satır olmasını sağlayan `CartItem` modeli oluşturuldu.
- Miktar 1–99 aralığında hem modelde hem SQL check constraint ile sınırlandı.
- Aktif olmayan ürün/kategori ve yetersiz stok kontrolleri API servisinde uygulandı.
- Sepet endpointleri `CustomerOnly` policy ile yalnızca `Kunde` rolüne açıldı.
- MVC, authentication cookie'yi mevcut delegating handler üzerinden API'ye aktardı.
- MVC mutasyon formları POST ve antiforgery token kullanıyor.
- Navbar'a bütün ziyaretçiler için görünür, ikonlu `Sepetim` bağlantısı eklendi. Yetki kontrolü hedef sayfa ve API'de yapılır.
- Yetkisiz rol için erişim reddi ekranı eklendi.
- Development testleri için `Kunde` seed hesabı eklendi.
- `AddShoppingCart` migration'ı oluşturuldu ve uygulandı.

# 4. CLI Komutları

Çalışma dizini: `SecureShop/`

```powershell
dotnet ef migrations add AddShoppingCart --project src/SecureShop.Api/SecureShop.Api.csproj --startup-project src/SecureShop.Api/SecureShop.Api.csproj --configuration Release --output-dir Data/Migrations
dotnet build SecureShop.sln --configuration Release
dotnet ef database update --project src/SecureShop.Api/SecureShop.Api.csproj --startup-project src/SecureShop.Api/SecureShop.Api.csproj --configuration Release --no-build
dotnet ef migrations has-pending-model-changes --project src/SecureShop.Api/SecureShop.Api.csproj --startup-project src/SecureShop.Api/SecureShop.Api.csproj --configuration Release --no-build
```

Development Kunde ayarları yalnızca git tarafından yok sayılan `.env` dosyasında tutulur:

```dotenv
SeedUsers__Customer__Email=customer@secureshop.local
SeedUsers__Customer__Password="<DEVELOPMENT_CUSTOMER_PASSWORD>"
SeedUsers__Customer__FirstName=Development
SeedUsers__Customer__LastName=Customer
```

# 5. Güncel Proje Yapısı

```text
src/
├── SecureShop.Api/
│   ├── Contracts/Requests/
│   │   ├── AddCartItemRequest.cs
│   │   └── UpdateCartItemQuantityRequest.cs
│   ├── Contracts/Responses/
│   │   ├── CartItemResponse.cs
│   │   └── CartResponse.cs
│   ├── Controllers/CartController.cs
│   ├── Data/
│   │   ├── Configurations/
│   │   │   ├── CartConfiguration.cs
│   │   │   └── CartItemConfiguration.cs
│   │   └── Migrations/20260716221538_AddShoppingCart.cs
│   ├── Domain/Entities/
│   │   ├── Cart.cs
│   │   └── CartItem.cs
│   ├── Features/Cart/
│   │   ├── ICartService.cs
│   │   ├── CartMutationResult.cs
│   │   └── CartService.cs
│   └── Security/
│       ├── ICurrentUserService.cs
│       └── CurrentUserService.cs
└── SecureShop.Mvc/
    ├── Controllers/CartController.cs
    ├── Models/Requests/
    │   ├── AddCartItemRequest.cs
    │   └── UpdateCartItemQuantityRequest.cs
    ├── Models/Responses/
    │   ├── CartItemResponse.cs
    │   └── CartResponse.cs
    ├── Models/ViewModels/CartViewModel.cs
    ├── Services/
    │   ├── Interfaces/ICartApiService.cs
    │   └── Api/CartApiService.cs
    ├── Security/AppRoles.cs
    └── Views/Cart/Index.cshtml
```

# 6. Dosya Bazında Eksiksiz Kodlar

Aşağıdaki içerikler bu adım sonunda kaynak dosyalarda bulunan eksiksiz kodlardır. EF tarafından üretilen migration designer ve model snapshot dosyaları ayrıca listelenmez; bunlar migration komutuyla otomatik üretilir.

## `src/SecureShop.Api/Domain/Entities/Cart.cs`

Uzantı: `.cs`

```csharp
namespace SecureShop.Api.Domain.Entities;

public sealed class Cart
{
    private Cart()
    {
    }

    public Cart(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "Kullanıcı kimliği boş olamaz.",
                nameof(userId));
        }

        Id = Guid.NewGuid();
        UserId = userId;
        CreatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public ICollection<CartItem> Items { get; private set; } =
        new List<CartItem>();

    public CartItem AddItem(Guid productId, int quantity)
    {
        var existingItem = Items.SingleOrDefault(
            item => item.ProductId == productId);

        if (existingItem is not null)
        {
            existingItem.SetQuantity(existingItem.Quantity + quantity);
            Touch();
            return existingItem;
        }

        var item = new CartItem(Id, productId, quantity);
        Items.Add(item);
        Touch();
        return item;
    }

    public void SetItemQuantity(CartItem item, int quantity)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.CartId != Id)
        {
            throw new InvalidOperationException(
                "Sepet öğesi bu sepete ait değil.");
        }

        item.SetQuantity(quantity);
        Touch();
    }

    public void RemoveItem(CartItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!Items.Remove(item))
        {
            throw new InvalidOperationException(
                "Sepet öğesi bu sepete ait değil.");
        }

        Touch();
    }

    public void Clear()
    {
        Items.Clear();
        Touch();
    }

    private void Touch()
    {
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
```

## `src/SecureShop.Api/Domain/Entities/CartItem.cs`

Uzantı: `.cs`

```csharp
namespace SecureShop.Api.Domain.Entities;

public sealed class CartItem
{
    private const int MaximumQuantity = 99;

    private CartItem()
    {
    }

    internal CartItem(
        Guid cartId,
        Guid productId,
        int quantity)
    {
        if (cartId == Guid.Empty)
        {
            throw new ArgumentException(
                "Sepet kimliği boş olamaz.",
                nameof(cartId));
        }

        if (productId == Guid.Empty)
        {
            throw new ArgumentException(
                "Ürün kimliği boş olamaz.",
                nameof(productId));
        }

        Id = Guid.NewGuid();
        CartId = cartId;
        ProductId = productId;
        SetQuantity(quantity);
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid CartId { get; private set; }

    public Guid ProductId { get; private set; }

    public int Quantity { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public Cart Cart { get; private set; } = null!;

    public Product Product { get; private set; } = null!;

    public void SetQuantity(int quantity)
    {
        if (quantity is < 1 or > MaximumQuantity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                $"Sepet miktarı 1 ile {MaximumQuantity} arasında olmalıdır.");
        }

        Quantity = quantity;
    }
}
```

## `src/SecureShop.Api/Data/Configurations/CartConfiguration.cs`

Uzantı: `.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecureShop.Api.Domain.Entities;
using SecureShop.Api.Security.Identity;

namespace SecureShop.Api.Data.Configurations;

public sealed class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.ToTable("Carts");

        builder.HasKey(cart => cart.Id);

        builder.Property(cart => cart.Id)
            .ValueGeneratedNever();

        builder.Property(cart => cart.UserId)
            .IsRequired();

        builder.Property(cart => cart.CreatedAtUtc)
            .HasPrecision(0)
            .IsRequired();

        builder.Property(cart => cart.UpdatedAtUtc)
            .HasPrecision(0)
            .IsRequired();

        builder.Property(cart => cart.RowVersion)
            .IsRowVersion();

        builder.HasIndex(cart => cart.UserId)
            .IsUnique()
            .HasDatabaseName("UX_Carts_UserId");

        builder.HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<Cart>(cart => cart.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
```

## `src/SecureShop.Api/Data/Configurations/CartItemConfiguration.cs`

Uzantı: `.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecureShop.Api.Domain.Entities;

namespace SecureShop.Api.Data.Configurations;

public sealed class CartItemConfiguration
    : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable(
            "CartItems",
            tableBuilder => tableBuilder.HasCheckConstraint(
                "CK_CartItems_Quantity_Range",
                "[Quantity] BETWEEN 1 AND 99"));

        builder.HasKey(item => item.Id);

        builder.Property(item => item.Id)
            .ValueGeneratedNever();

        builder.Property(item => item.CartId)
            .IsRequired();

        builder.Property(item => item.ProductId)
            .IsRequired();

        builder.Property(item => item.Quantity)
            .IsRequired();

        builder.Property(item => item.CreatedAtUtc)
            .HasPrecision(0)
            .IsRequired();

        builder.Property(item => item.RowVersion)
            .IsRowVersion();

        builder.HasIndex(item => new
        {
            item.CartId,
            item.ProductId
        })
            .IsUnique()
            .HasDatabaseName("UX_CartItems_CartId_ProductId");

        builder.HasOne(item => item.Cart)
            .WithMany(cart => cart.Items)
            .HasForeignKey(item => item.CartId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasOne(item => item.Product)
            .WithMany()
            .HasForeignKey(item => item.ProductId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}
```

## `src/SecureShop.Api/Contracts/Requests/AddCartItemRequest.cs`

Uzantı: `.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace SecureShop.Api.Contracts.Requests;

public sealed class AddCartItemRequest
{
    [Required]
    public Guid ProductId { get; init; }

    [Range(1, 99)]
    public int Quantity { get; init; } = 1;
}
```

## `src/SecureShop.Api/Contracts/Requests/UpdateCartItemQuantityRequest.cs`

Uzantı: `.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace SecureShop.Api.Contracts.Requests;

public sealed class UpdateCartItemQuantityRequest
{
    [Range(1, 99)]
    public int Quantity { get; init; }
}
```

## `src/SecureShop.Api/Contracts/Responses/CartItemResponse.cs`

Uzantı: `.cs`

```csharp
namespace SecureShop.Api.Contracts.Responses;

public sealed record CartItemResponse(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal,
    int AvailableStock,
    bool IsAvailable);
```

## `src/SecureShop.Api/Contracts/Responses/CartResponse.cs`

Uzantı: `.cs`

```csharp
namespace SecureShop.Api.Contracts.Responses;

public sealed record CartResponse(
    Guid? Id,
    IReadOnlyList<CartItemResponse> Items,
    int TotalQuantity,
    decimal TotalAmount,
    DateTimeOffset? UpdatedAtUtc);
```

## `src/SecureShop.Api/Security/ICurrentUserService.cs`

Uzantı: `.cs`

```csharp
namespace SecureShop.Api.Security;

public interface ICurrentUserService
{
    Guid? UserId { get; }
}
```

## `src/SecureShop.Api/Security/CurrentUserService.cs`

Uzantı: `.cs`

```csharp
using System.Security.Claims;

namespace SecureShop.Api.Security;

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(
        IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var value = _httpContextAccessor
                .HttpContext?
                .User
                .FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(value, out var userId)
                ? userId
                : null;
        }
    }
}
```

## `src/SecureShop.Api/Features/Cart/ICartService.cs`

Uzantı: `.cs`

```csharp
using SecureShop.Api.Contracts.Responses;

namespace SecureShop.Api.Features.Cart;

public interface ICartService
{
    Task<CartResponse> GetAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<CartMutationResult> AddItemAsync(
        Guid userId,
        Guid productId,
        int quantity,
        CancellationToken cancellationToken);

    Task<CartMutationResult> UpdateItemAsync(
        Guid userId,
        Guid itemId,
        int quantity,
        CancellationToken cancellationToken);

    Task<CartMutationResult> RemoveItemAsync(
        Guid userId,
        Guid itemId,
        CancellationToken cancellationToken);

    Task<CartMutationResult> ClearAsync(
        Guid userId,
        CancellationToken cancellationToken);
}
```

## `src/SecureShop.Api/Features/Cart/CartMutationResult.cs`

Uzantı: `.cs`

```csharp
using SecureShop.Api.Contracts.Responses;

namespace SecureShop.Api.Features.Cart;

public enum CartMutationStatus
{
    Succeeded,
    ProductUnavailable,
    InsufficientStock,
    ItemNotFound,
    ConcurrencyConflict
}

public sealed record CartMutationResult(
    CartMutationStatus Status,
    CartResponse? Cart = null);
```

## `src/SecureShop.Api/Controllers/CartController.cs`

Uzantı: `.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureShop.Api.Contracts.Requests;
using SecureShop.Api.Contracts.Responses;
using SecureShop.Api.Features.Cart;
using SecureShop.Api.Security;
using SecureShop.Api.Security.Policies;

namespace SecureShop.Api.Controllers;

[ApiController]
[Route("api/cart")]
[Authorize(Policy = AppPolicies.CustomerOnly)]
public sealed class CartController : ControllerBase
{
    private readonly ICurrentUserService _currentUser;
    private readonly ICartService _cartService;

    public CartController(
        ICurrentUserService currentUser,
        ICartService cartService)
    {
        _currentUser = currentUser;
        _cartService = cartService;
    }

    [HttpGet]
    public async Task<ActionResult<CartResponse>> Get(
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not Guid userId)
        {
            return Unauthorized(CreateProblem(
                "Kimliği doğrulanmış kullanıcı bilgisi bulunamadı."));
        }

        return Ok(await _cartService.GetAsync(
            userId,
            cancellationToken));
    }

    [HttpPost("items")]
    public async Task<ActionResult<CartResponse>> AddItem(
        AddCartItemRequest request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not Guid userId)
        {
            return Unauthorized(CreateProblem(
                "Kimliği doğrulanmış kullanıcı bilgisi bulunamadı."));
        }

        var result = await _cartService.AddItemAsync(
            userId,
            request.ProductId,
            request.Quantity,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpPut("items/{itemId:guid}")]
    public async Task<ActionResult<CartResponse>> UpdateItem(
        Guid itemId,
        UpdateCartItemQuantityRequest request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not Guid userId)
        {
            return Unauthorized(CreateProblem(
                "Kimliği doğrulanmış kullanıcı bilgisi bulunamadı."));
        }

        var result = await _cartService.UpdateItemAsync(
            userId,
            itemId,
            request.Quantity,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpDelete("items/{itemId:guid}")]
    public async Task<ActionResult<CartResponse>> RemoveItem(
        Guid itemId,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not Guid userId)
        {
            return Unauthorized(CreateProblem(
                "Kimliği doğrulanmış kullanıcı bilgisi bulunamadı."));
        }

        var result = await _cartService.RemoveItemAsync(
            userId,
            itemId,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpDelete]
    public async Task<ActionResult<CartResponse>> Clear(
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not Guid userId)
        {
            return Unauthorized(CreateProblem(
                "Kimliği doğrulanmış kullanıcı bilgisi bulunamadı."));
        }

        var result = await _cartService.ClearAsync(
            userId,
            cancellationToken);

        return ToActionResult(result);
    }

    private ActionResult<CartResponse> ToActionResult(
        CartMutationResult result)
    {
        if (result.Status == CartMutationStatus.Succeeded
            && result.Cart is not null)
        {
            return Ok(result.Cart);
        }

        return result.Status switch
        {
            CartMutationStatus.ProductUnavailable =>
                BadRequest(CreateProblem(
                    "Ürün bulunamadı veya satışa açık değil.")),
            CartMutationStatus.InsufficientStock =>
                Conflict(CreateProblem(
                    "İstenen miktar kullanılabilir stok sınırını aşıyor.")),
            CartMutationStatus.ItemNotFound =>
                NotFound(CreateProblem("Sepet öğesi bulunamadı.")),
            CartMutationStatus.ConcurrencyConflict =>
                Conflict(CreateProblem(
                    "Sepet başka bir istek tarafından değiştirildi. Sayfayı yenileyin.")),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    private static ProblemDetails CreateProblem(string detail) =>
        new()
        {
            Detail = detail
        };
}
```

## `src/SecureShop.Api/Data/AppDbContext.cs`

Uzantı: `.cs`

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SecureShop.Api.Domain.Entities;
using SecureShop.Api.Security.Identity;

namespace SecureShop.Api.Data;

public sealed class AppDbContext
    : IdentityDbContext<
        ApplicationUser,
        ApplicationRole,
        Guid,
        IdentityUserClaim<Guid>,
        ApplicationUserRole,
        IdentityUserLogin<Guid>,
        IdentityRoleClaim<Guid>,
        IdentityUserToken<Guid>>
{
    public AppDbContext(
        DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Cart> Carts => Set<Cart>();

    public DbSet<CartItem> CartItems => Set<CartItem>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AppDbContext).Assembly);
    }
}
```

## `src/SecureShop.Api/Features/Cart/CartService.cs`

Uzantı: `.cs`

```csharp
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SecureShop.Api.Contracts.Responses;
using SecureShop.Api.Data;
using SecureShop.Api.Domain.Entities;

namespace SecureShop.Api.Features.Cart;

public sealed class CartService : ICartService
{
    private const int MaximumQuantity = 99;

    private readonly AppDbContext _dbContext;

    public CartService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CartResponse> GetAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var cart = await FindCartAsync(
            userId,
            tracking: false,
            cancellationToken);

        return cart is null
            ? EmptyCart()
            : Map(cart);
    }

    public async Task<CartMutationResult> AddItemAsync(
        Guid userId,
        Guid productId,
        int quantity,
        CancellationToken cancellationToken)
    {
        var product = await _dbContext.Products
            .Include(item => item.Category)
            .SingleOrDefaultAsync(
                item => item.Id == productId,
                cancellationToken);

        if (product is null
            || !product.IsActive
            || !product.Category.IsActive)
        {
            return new(CartMutationStatus.ProductUnavailable);
        }

        var cart = await FindCartAsync(
            userId,
            tracking: true,
            cancellationToken);

        if (cart is null)
        {
            cart = new Domain.Entities.Cart(userId);
            _dbContext.Carts.Add(cart);
        }

        var existingQuantity = cart.Items
            .SingleOrDefault(item => item.ProductId == productId)?
            .Quantity ?? 0;
        var requestedQuantity = existingQuantity + quantity;

        if (requestedQuantity > MaximumQuantity
            || requestedQuantity > product.StockQuantity)
        {
            return new(CartMutationStatus.InsufficientStock);
        }

        cart.AddItem(productId, quantity);

        return await SaveAsync(cart, cancellationToken);
    }

    public async Task<CartMutationResult> UpdateItemAsync(
        Guid userId,
        Guid itemId,
        int quantity,
        CancellationToken cancellationToken)
    {
        var cart = await FindCartAsync(
            userId,
            tracking: true,
            cancellationToken);
        var item = cart?.Items.SingleOrDefault(
            currentItem => currentItem.Id == itemId);

        if (cart is null || item is null)
        {
            return new(CartMutationStatus.ItemNotFound);
        }

        if (!IsProductAvailable(item.Product))
        {
            return new(CartMutationStatus.ProductUnavailable);
        }

        if (quantity > item.Product.StockQuantity)
        {
            return new(CartMutationStatus.InsufficientStock);
        }

        cart.SetItemQuantity(item, quantity);

        return await SaveAsync(cart, cancellationToken);
    }

    public async Task<CartMutationResult> RemoveItemAsync(
        Guid userId,
        Guid itemId,
        CancellationToken cancellationToken)
    {
        var cart = await FindCartAsync(
            userId,
            tracking: true,
            cancellationToken);
        var item = cart?.Items.SingleOrDefault(
            currentItem => currentItem.Id == itemId);

        if (cart is null || item is null)
        {
            return new(CartMutationStatus.ItemNotFound);
        }

        cart.RemoveItem(item);

        return await SaveAsync(cart, cancellationToken);
    }

    public async Task<CartMutationResult> ClearAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var cart = await FindCartAsync(
            userId,
            tracking: true,
            cancellationToken);

        if (cart is null)
        {
            return new(
                CartMutationStatus.Succeeded,
                EmptyCart());
        }

        cart.Clear();

        return await SaveAsync(cart, cancellationToken);
    }

    private async Task<CartMutationResult> SaveAsync(
        Domain.Entities.Cart cart,
        CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(CartMutationStatus.ConcurrencyConflict);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException
            {
                Number: 2601 or 2627
            })
        {
            return new(CartMutationStatus.ConcurrencyConflict);
        }

        await ReloadProductsAsync(cart, cancellationToken);

        return new(
            CartMutationStatus.Succeeded,
            Map(cart));
    }

    private Task<Domain.Entities.Cart?> FindCartAsync(
        Guid userId,
        bool tracking,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Carts
            .Include(cart => cart.Items)
                .ThenInclude(item => item.Product)
                    .ThenInclude(product => product.Category)
            .AsSplitQuery();

        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return query.SingleOrDefaultAsync(
            cart => cart.UserId == userId,
            cancellationToken);
    }

    private async Task ReloadProductsAsync(
        Domain.Entities.Cart cart,
        CancellationToken cancellationToken)
    {
        foreach (var item in cart.Items)
        {
            await _dbContext.Entry(item)
                .Reference(currentItem => currentItem.Product)
                .Query()
                .Include(product => product.Category)
                .LoadAsync(cancellationToken);
        }
    }

    private static bool IsProductAvailable(Product product) =>
        product.IsActive
        && product.Category.IsActive
        && product.StockQuantity > 0;

    private static CartResponse EmptyCart() =>
        new(
            Id: null,
            Items: [],
            TotalQuantity: 0,
            TotalAmount: 0m,
            UpdatedAtUtc: null);

    private static CartResponse Map(Domain.Entities.Cart cart)
    {
        var items = cart.Items
            .OrderBy(item => item.Product.Name)
            .Select(item =>
            {
                var lineTotal = decimal.Round(
                    item.Product.Price * item.Quantity,
                    2,
                    MidpointRounding.ToEven);

                return new CartItemResponse(
                    item.Id,
                    item.ProductId,
                    item.Product.Name,
                    item.Product.Sku,
                    item.Product.Price,
                    item.Quantity,
                    lineTotal,
                    item.Product.StockQuantity,
                    IsProductAvailable(item.Product));
            })
            .ToList();

        return new CartResponse(
            cart.Id,
            items,
            items.Sum(item => item.Quantity),
            items.Sum(item => item.LineTotal),
            cart.UpdatedAtUtc);
    }
}
```

## `src/SecureShop.Api/Program.cs`

Uzantı: `.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using SecureShop.Api.Configuration;
using SecureShop.Api.Data;
using SecureShop.Api.Data.Seed;
using SecureShop.Api.Domain.Constants;
using SecureShop.Api.Features.Auth.External;
using SecureShop.Api.Features.Auth.TwoFactor;
using SecureShop.Api.Features.Cart;
using SecureShop.Api.Features.Products;
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
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICartService, CartService>();

builder.Services.AddControllers();
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
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();
app.UseRateLimiter();

app.UseCookiePolicy();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await app.RunAsync();
```

## `src/SecureShop.Api/Data/Seed/IdentitySeeder.cs`

Uzantı: `.cs`

```csharp
using Microsoft.AspNetCore.Identity;
using SecureShop.Api.Domain.Constants;
using SecureShop.Api.Security.Identity;

namespace SecureShop.Api.Data.Seed;

public sealed class IdentitySeeder
{
    private static readonly IReadOnlyCollection<RoleSeedDefinition>
        RoleDefinitions =
        [
            new(
                AppRoles.Admin,
                "Sistem yönetimi ve tüm yönetim işlemleri.",
                true),

            new(
                AppRoles.Employee,
                "Ürün, stok ve operasyon işlemlerini yönetir.",
                true),

            new(
                AppRoles.Kunde,
                "Müşteri alışveriş, sepet ve sipariş işlemleri.",
                true)
        ];

    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<IdentitySeeder> _logger;

    public IdentitySeeder(
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<IdentitySeeder> logger)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        await EnsureRolesAsync();

        if (!_environment.IsDevelopment())
        {
            return;
        }

        await EnsureDevelopmentUserAsync(
            sectionName: "SeedUsers:Admin",
            roleName: AppRoles.Admin,
            defaultFirstName: "System",
            defaultLastName: "Administrator");

        await EnsureDevelopmentUserAsync(
            sectionName: "SeedUsers:Employee",
            roleName: AppRoles.Employee,
            defaultFirstName: "Development",
            defaultLastName: "Employee");

        await EnsureDevelopmentUserAsync(
            sectionName: "SeedUsers:Customer",
            roleName: AppRoles.Kunde,
            defaultFirstName: "Development",
            defaultLastName: "Customer");
    }

    private async Task EnsureRolesAsync()
    {
        foreach (var definition in RoleDefinitions)
        {
            var role = await _roleManager.FindByNameAsync(
                definition.Name);

            if (role is null)
            {
                role = new ApplicationRole(
                    definition.Name,
                    definition.Description,
                    definition.IsSystem);

                var createResult = await _roleManager.CreateAsync(role);

                ThrowIfFailed(
                    createResult,
                    $"Role '{definition.Name}' could not be created.");

                _logger.LogInformation(
                    "Identity role created: {RoleName}",
                    definition.Name);

                continue;
            }

            var wasChanged = role.SetMetadata(
                definition.Description,
                definition.IsSystem);

            if (!wasChanged)
            {
                continue;
            }

            var updateResult = await _roleManager.UpdateAsync(role);

            ThrowIfFailed(
                updateResult,
                $"Role '{definition.Name}' could not be updated.");

            _logger.LogInformation(
                "Identity role updated: {RoleName}",
                definition.Name);
        }
    }

    private async Task EnsureDevelopmentUserAsync(
        string sectionName,
        string roleName,
        string defaultFirstName,
        string defaultLastName)
    {
        var email = _configuration[$"{sectionName}:Email"]?.Trim();
        var password = _configuration[$"{sectionName}:Password"];
        var firstName = GetConfiguredName(
            $"{sectionName}:FirstName",
            defaultFirstName);
        var lastName = GetConfiguredName(
            $"{sectionName}:LastName",
            defaultLastName);

        if (string.IsNullOrWhiteSpace(email)
            && string.IsNullOrWhiteSpace(password))
        {
            _logger.LogInformation(
                "{RoleName} development user was skipped because it is not configured.",
                roleName);

            return;
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException(
                $"{sectionName}:Email configuration was not found.");
        }

        var user = await _userManager.FindByEmailAsync(email);

        if (user is null)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    $"{sectionName}:Password configuration was not found.");
            }

            user = new ApplicationUser(
                email,
                firstName,
                lastName)
            {
                EmailConfirmed = true
            };

            var createResult = await _userManager.CreateAsync(
                user,
                password);

            ThrowIfFailed(
                createResult,
                $"{roleName} development user could not be created.");

            _logger.LogInformation(
                "{RoleName} development user created.",
                roleName);
        }
        else if (!string.IsNullOrWhiteSpace(password))
        {
            var resetToken =
                await _userManager.GeneratePasswordResetTokenAsync(user);

            var resetResult = await _userManager.ResetPasswordAsync(
                user,
                resetToken,
                password);

            ThrowIfFailed(
                resetResult,
                $"{roleName} development user password could not be synchronized.");

            var resetAccessFailedResult =
                await _userManager.ResetAccessFailedCountAsync(user);

            ThrowIfFailed(
                resetAccessFailedResult,
                $"{roleName} development user access-failed count could not be reset.");

            var clearLockoutResult =
                await _userManager.SetLockoutEndDateAsync(user, null);

            ThrowIfFailed(
                clearLockoutResult,
                $"{roleName} development user lockout could not be cleared.");

            if (!await _userManager.CheckPasswordAsync(user, password))
            {
                throw new InvalidOperationException(
                    $"{roleName} development user password synchronization verification failed.");
            }

            _logger.LogInformation(
                "{RoleName} development user password synchronized from development configuration.",
                roleName);
        }

        if (await _userManager.IsInRoleAsync(user, roleName))
        {
            return;
        }

        var addToRoleResult = await _userManager.AddToRoleAsync(
            user,
            roleName);

        ThrowIfFailed(
            addToRoleResult,
            $"Development user could not be added to role '{roleName}'.");

        _logger.LogInformation(
            "Development user added to {RoleName} role.",
            roleName);
    }

    private string GetConfiguredName(
        string configurationKey,
        string defaultValue)
    {
        var configuredValue = _configuration[configurationKey]?.Trim();

        return string.IsNullOrWhiteSpace(configuredValue)
            ? defaultValue
            : configuredValue;
    }

    private static void ThrowIfFailed(
        IdentityResult result,
        string message)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join(
            "; ",
            result.Errors.Select(error =>
                $"{error.Code}: {error.Description}"));

        throw new InvalidOperationException(
            $"{message} {errors}");
    }

    private sealed record RoleSeedDefinition(
        string Name,
        string Description,
        bool IsSystem);
}
```

## `src/SecureShop.Api/Data/Migrations/20260716221538_AddShoppingCart.cs`

Uzantı: `.cs`

```csharp
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureShop.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShoppingCart : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Carts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Carts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Carts_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CartItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CartItems", x => x.Id);
                    table.CheckConstraint("CK_CartItems_Quantity_Range", "[Quantity] BETWEEN 1 AND 99");
                    table.ForeignKey(
                        name: "FK_CartItems_Carts_CartId",
                        column: x => x.CartId,
                        principalTable: "Carts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CartItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_ProductId",
                table: "CartItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "UX_CartItems_CartId_ProductId",
                table: "CartItems",
                columns: new[] { "CartId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Carts_UserId",
                table: "Carts",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CartItems");

            migrationBuilder.DropTable(
                name: "Carts");
        }
    }
}
```

## `src/SecureShop.Mvc/Security/AppRoles.cs`

Uzantı: `.cs`

```csharp
namespace SecureShop.Mvc.Security;

public static class AppRoles
{
    public const string Customer = "Kunde";
}
```

## `src/SecureShop.Mvc/Models/Requests/AddCartItemRequest.cs`

Uzantı: `.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace SecureShop.Mvc.Models.Requests;

public sealed class AddCartItemRequest
{
    [Required]
    public Guid ProductId { get; init; }

    [Range(1, 99, ErrorMessage = "Adet 1 ile 99 arasında olmalıdır.")]
    public int Quantity { get; init; } = 1;
}
```

## `src/SecureShop.Mvc/Models/Requests/UpdateCartItemQuantityRequest.cs`

Uzantı: `.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace SecureShop.Mvc.Models.Requests;

public sealed class UpdateCartItemQuantityRequest
{
    [Range(1, 99, ErrorMessage = "Adet 1 ile 99 arasında olmalıdır.")]
    public int Quantity { get; init; }
}
```

## `src/SecureShop.Mvc/Models/Responses/CartItemResponse.cs`

Uzantı: `.cs`

```csharp
namespace SecureShop.Mvc.Models.Responses;

public sealed record CartItemResponse(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal,
    int AvailableStock,
    bool IsAvailable);
```

## `src/SecureShop.Mvc/Models/Responses/CartResponse.cs`

Uzantı: `.cs`

```csharp
namespace SecureShop.Mvc.Models.Responses;

public sealed record CartResponse(
    Guid? Id,
    IReadOnlyList<CartItemResponse> Items,
    int TotalQuantity,
    decimal TotalAmount,
    DateTimeOffset? UpdatedAtUtc);
```

## `src/SecureShop.Mvc/Models/ViewModels/CartViewModel.cs`

Uzantı: `.cs`

```csharp
using SecureShop.Mvc.Models.Responses;

namespace SecureShop.Mvc.Models.ViewModels;

public sealed class CartViewModel
{
    public CartResponse? Cart { get; init; }

    public string? ErrorMessage { get; init; }
}
```

## `src/SecureShop.Mvc/Services/Interfaces/ICartApiService.cs`

Uzantı: `.cs`

```csharp
using SecureShop.Mvc.Http;
using SecureShop.Mvc.Models.Requests;
using SecureShop.Mvc.Models.Responses;

namespace SecureShop.Mvc.Services.Interfaces;

public interface ICartApiService
{
    Task<ApiResponse<CartResponse>> GetAsync(
        CancellationToken cancellationToken = default);

    Task<ApiResponse<CartResponse>> AddItemAsync(
        AddCartItemRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<CartResponse>> UpdateItemAsync(
        Guid itemId,
        UpdateCartItemQuantityRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<CartResponse>> RemoveItemAsync(
        Guid itemId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<CartResponse>> ClearAsync(
        CancellationToken cancellationToken = default);
}
```

## `src/SecureShop.Mvc/Services/Api/CartApiService.cs`

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

public sealed class CartApiService : ICartApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CartApiService> _logger;

    public CartApiService(
        HttpClient httpClient,
        ILogger<CartApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<ApiResponse<CartResponse>> GetAsync(
        CancellationToken cancellationToken = default) =>
        SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "api/cart"),
            cancellationToken);

    public Task<ApiResponse<CartResponse>> AddItemAsync(
        AddCartItemRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            new HttpRequestMessage(HttpMethod.Post, "api/cart/items")
            {
                Content = JsonContent.Create(request)
            },
            cancellationToken);

    public Task<ApiResponse<CartResponse>> UpdateItemAsync(
        Guid itemId,
        UpdateCartItemQuantityRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            new HttpRequestMessage(
                HttpMethod.Put,
                $"api/cart/items/{itemId:D}")
            {
                Content = JsonContent.Create(request)
            },
            cancellationToken);

    public Task<ApiResponse<CartResponse>> RemoveItemAsync(
        Guid itemId,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            new HttpRequestMessage(
                HttpMethod.Delete,
                $"api/cart/items/{itemId:D}"),
            cancellationToken);

    public Task<ApiResponse<CartResponse>> ClearAsync(
        CancellationToken cancellationToken = default) =>
        SendAsync(
            new HttpRequestMessage(HttpMethod.Delete, "api/cart"),
            cancellationToken);

    private async Task<ApiResponse<CartResponse>> SendAsync(
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
                    return ApiResponse<CartResponse>.Failure(
                        response.StatusCode,
                        await GetErrorMessageAsync(
                            response,
                            cancellationToken));
                }

                var cart = await response.Content
                    .ReadFromJsonAsync<CartResponse>(
                        cancellationToken: cancellationToken);

                return cart is null
                    ? ApiResponse<CartResponse>.Failure(
                        HttpStatusCode.BadGateway,
                        "API geçerli bir sepet response'u döndürmedi.")
                    : ApiResponse<CartResponse>.Success(
                        response.StatusCode,
                        cart);
            }
            catch (HttpRequestException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Sepet API isteği tamamlanamadı.");

                return ApiResponse<CartResponse>.Failure(
                    HttpStatusCode.ServiceUnavailable,
                    "SecureShop API hizmetine ulaşılamıyor.");
            }
            catch (JsonException exception)
            {
                _logger.LogError(
                    exception,
                    "Sepet API response'u okunamadı.");

                return ApiResponse<CartResponse>.Failure(
                    HttpStatusCode.BadGateway,
                    "API sepet response formatı geçersiz.");
            }
            catch (OperationCanceledException)
                when (!cancellationToken.IsCancellationRequested)
            {
                return ApiResponse<CartResponse>.Failure(
                    HttpStatusCode.GatewayTimeout,
                    "Sepet API isteği zaman aşımına uğradı.");
            }
        }
    }

    private static async Task<string> GetErrorMessageAsync(
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
            return GetFallbackErrorMessage(response.StatusCode);
        }

        return GetFallbackErrorMessage(response.StatusCode);
    }

    private static string GetFallbackErrorMessage(
        HttpStatusCode statusCode) =>
        statusCode switch
        {
            HttpStatusCode.Unauthorized =>
                "Sepeti görüntülemek için giriş yapmalısınız.",
            HttpStatusCode.Forbidden =>
                "Sepet işlemleri yalnızca müşteri hesaplarına açıktır.",
            HttpStatusCode.NotFound =>
                "Sepet öğesi bulunamadı.",
            HttpStatusCode.Conflict =>
                "Sepet güncellenemedi. Stok durumunu kontrol edin.",
            _ => "Sepet işlemi tamamlanamadı."
        };
}
```

## `src/SecureShop.Mvc/Controllers/CartController.cs`

Uzantı: `.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureShop.Mvc.Models.Requests;
using SecureShop.Mvc.Models.ViewModels;
using SecureShop.Mvc.Security;
using SecureShop.Mvc.Services.Interfaces;

namespace SecureShop.Mvc.Controllers;

[Authorize(Roles = AppRoles.Customer)]
[Route("cart")]
public sealed class CartController : Controller
{
    private readonly ICartApiService _cartApiService;

    public CartController(ICartApiService cartApiService)
    {
        _cartApiService = cartApiService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken)
    {
        var result = await _cartApiService.GetAsync(cancellationToken);

        return View(new CartViewModel
        {
            Cart = result.Data,
            ErrorMessage = result.ErrorMessage
        });
    }

    [HttpPost("items")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(
        AddCartItemRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] =
                "Geçerli bir ürün ve adet seçin.";

            return RedirectToAction(nameof(Index));
        }

        var result = await _cartApiService.AddItemAsync(
            request,
            cancellationToken);

        SetResultMessage(
            result.IsSuccess,
            result.ErrorMessage,
            "Ürün sepete eklendi.");

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("items/{itemId:guid}/quantity")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(
        Guid itemId,
        UpdateCartItemQuantityRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] =
                "Adet 1 ile 99 arasında olmalıdır.";

            return RedirectToAction(nameof(Index));
        }

        var result = await _cartApiService.UpdateItemAsync(
            itemId,
            request,
            cancellationToken);

        SetResultMessage(
            result.IsSuccess,
            result.ErrorMessage,
            "Sepet miktarı güncellendi.");

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("items/{itemId:guid}/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(
        Guid itemId,
        CancellationToken cancellationToken)
    {
        var result = await _cartApiService.RemoveItemAsync(
            itemId,
            cancellationToken);

        SetResultMessage(
            result.IsSuccess,
            result.ErrorMessage,
            "Ürün sepetten çıkarıldı.");

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("clear")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Clear(
        CancellationToken cancellationToken)
    {
        var result = await _cartApiService.ClearAsync(
            cancellationToken);

        SetResultMessage(
            result.IsSuccess,
            result.ErrorMessage,
            "Sepet temizlendi.");

        return RedirectToAction(nameof(Index));
    }

    private void SetResultMessage(
        bool succeeded,
        string? errorMessage,
        string successMessage)
    {
        TempData[succeeded ? "SuccessMessage" : "ErrorMessage"] =
            succeeded
                ? successMessage
                : errorMessage ?? "Sepet işlemi tamamlanamadı.";
    }
}
```

## `src/SecureShop.Mvc/Views/Cart/Index.cshtml`

Uzantı: `.cshtml`

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
                                       asp-route-id="@item.ProductId"
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

## `src/SecureShop.Mvc/Views/Account/Forbidden.cshtml`

Uzantı: `.cshtml`

```cshtml
@{
    ViewData["Title"] = "Erişim reddedildi";
}

<section class="card border-0 shadow-sm mx-auto" style="max-width: 42rem;">
    <div class="card-body p-5 text-center">
        <div class="display-5 mb-3" aria-hidden="true">🔒</div>
        <h1 class="h3">Bu sayfaya erişim yetkiniz yok</h1>
        <p class="text-body-secondary">
            Sepet işlemleri yalnızca müşteri hesaplarına açıktır.
        </p>
        <a asp-controller="Home" asp-action="Index" class="btn btn-primary">
            Ana sayfaya dön
        </a>
    </div>
</section>
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
    public IActionResult Login()
    {
        return User.Identity?.IsAuthenticated == true
            ? RedirectToAction(nameof(Session))
            : View(new LoginViewModel());
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

        return RedirectToAction(nameof(Session));
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
    public IActionResult Forbidden()
    {
        return View();
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
}
```

## `src/SecureShop.Mvc/Views/Products/Details.cshtml`

Uzantı: `.cshtml`

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
        <div class="d-flex flex-wrap gap-2 align-items-end">
            <a asp-action="Index" class="btn btn-outline-secondary">Ürünlere dön</a>

            @if (User.IsInRole(AppRoles.Customer) && Model.StockQuantity > 0)
            {
                <form asp-controller="Cart"
                      asp-action="Add"
                      method="post"
                      class="d-flex gap-2 align-items-end">
                    <input type="hidden" name="ProductId" value="@Model.Id" />
                    <div>
                        <label for="cart-quantity" class="form-label mb-1">Adet</label>
                        <input id="cart-quantity"
                               name="Quantity"
                               type="number"
                               min="1"
                               max="@Math.Min(Model.StockQuantity, 99)"
                               value="1"
                               class="form-control cart-quantity-input" />
                    </div>
                    <button type="submit" class="btn btn-primary">Sepete ekle</button>
                </form>
            }
            else if (User.Identity?.IsAuthenticated != true)
            {
                <a asp-controller="Account" asp-action="Login" class="btn btn-primary">
                    Sepete eklemek için giriş yap
                </a>
            }
        </div>
    </div>
</article>
```

## `src/SecureShop.Mvc/Views/Shared/_Layout.cshtml`

Uzantı: `.cshtml`

```cshtml
<!DOCTYPE html>
<html lang="tr">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>@ViewData["Title"] - SecureShop</title>
    <script type="importmap"></script>
    <link rel="stylesheet" href="~/lib/bootstrap/dist/css/bootstrap.min.css" />
    <link rel="stylesheet" href="~/css/site.css" asp-append-version="true" />
    <link rel="stylesheet" href="~/SecureShop.Mvc.styles.css" asp-append-version="true" />
</head>
<body>
    <header>
        <nav class="navbar navbar-expand-sm navbar-dark secure-navbar shadow-sm mb-4">
            <div class="container">
                <a class="navbar-brand secure-brand" asp-controller="Home" asp-action="Index">
                    <span class="brand-mark" aria-hidden="true">S</span>
                    <span>SecureShop</span>
                </a>
                <button class="navbar-toggler" type="button" data-bs-toggle="collapse" data-bs-target=".navbar-collapse" aria-controls="navbarSupportedContent"
                        aria-expanded="false" aria-label="Toggle navigation">
                    <span class="navbar-toggler-icon"></span>
                </button>
                <div id="navbarSupportedContent" class="navbar-collapse collapse">
                    <ul class="navbar-nav me-auto">
                        <li class="nav-item">
                            <a class="nav-link" asp-controller="Home" asp-action="Index">Ana Sayfa</a>
                        </li>
                        <li class="nav-item">
                            <a class="nav-link" asp-controller="Products" asp-action="Index">Ürünler</a>
                        </li>
                        <li class="nav-item">
                            <a class="nav-link nav-cart-link"
                               asp-controller="Cart"
                               asp-action="Index"
                               aria-label="Sepetim">
                                <span aria-hidden="true">🛒</span>
                                <span>Sepetim</span>
                            </a>
                        </li>
                    </ul>
                    <partial name="_AuthenticationMenu" />
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
            &copy; 2026 SecureShop
        </div>
    </footer>
    <script src="~/lib/jquery/dist/jquery.min.js"></script>
    <script src="~/lib/bootstrap/dist/js/bootstrap.bundle.min.js"></script>
    <script src="~/js/site.js" asp-append-version="true"></script>
    @await RenderSectionAsync("Scripts", required: false)
</body>
</html>
```

## `src/SecureShop.Mvc/Views/_ViewImports.cshtml`

Uzantı: `.cshtml`

```cshtml
@using SecureShop.Mvc
@using SecureShop.Mvc.Models
@using SecureShop.Mvc.Security
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
```

## `src/SecureShop.Mvc/Program.cs`

Uzantı: `.cs`

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

## `src/SecureShop.Mvc/wwwroot/css/site.css`

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

- `UX_Carts_UserId`, bir kullanıcının ikinci sepet oluşturmasını veritabanı seviyesinde engeller.
- `UX_CartItems_CartId_ProductId`, aynı ürünün sepette iki ayrı satıra dönüşmesini engeller.
- `CurrentUserService`, kullanıcı kimliğini yalnızca API authentication context'inden okur.
- `CartService`, ürünün aktifliği, kategorinin aktifliği, stok ve maksimum adet kurallarının tek yetkili uygulayıcısıdır.
- Sepette fiyat saklanmaz; response hazırlanırken ürünün güncel fiyatı kullanılır. Sipariş adımında fiyat ve stok tekrar doğrulanacaktır.
- MVC'deki rol kontrolü kullanıcı deneyimi içindir. Gerçek authorization API'deki `CustomerOnly` policy'dir.
- API mutasyonları JSON bekler. MVC formları antiforgery ile korunur ve cookie server-to-server API isteğine aktarılır.

# 8. API–MVC Veri Akışı

```text
Razor Product/Cart View
    ↓ POST + antiforgery
MVC CartController
    ↓
ICartApiService
    ↓ authentication cookie forwarding
HttpClient
    ↓ JSON
API CartController
    ↓ CustomerOnly policy + NameIdentifier claim
ICartService
    ↓ ürün, kategori, stok ve kullanıcı kontrolleri
AppDbContext
    ↓
SQL Server (Carts, CartItems, Products)
```

API'ye gönderilen ekleme isteği yalnızca şudur:

```json
{
  "productId": "00000000-0000-0000-0000-000000000000",
  "quantity": 2
}
```

`userId`, fiyat, rol, stok veya toplam tutar request içinde bulunmaz.

# 9. Uygulama Sırası

1. `Cart` ve `CartItem` entity'leri.
2. EF Core configuration ve `AppDbContext` DbSet'leri.
3. Request/response contract'ları.
4. Current-user ve cart servisleri.
5. Policy korumalı API controller.
6. EF migration ve database update.
7. MVC request/response modelleri.
8. Typed API service ve MVC controller.
9. Razor sepet görünümü, ürün formu ve navbar bağlantısı.
10. Build, migration kontrolü ve uçtan uca test.

# 10. Çalıştırma ve Test

Terminal 1 — çalışma dizini: `SecureShop/src/SecureShop.Api`

```powershell
dotnet watch run --launch-profile https
```

Terminal 2 — çalışma dizini: `SecureShop/src/SecureShop.Mvc`

```powershell
dotnet watch run --launch-profile https
```

Tarayıcı testi:

1. `https://localhost:7002/account/login` adresinde development Kunde hesabıyla giriş yap.
2. Navbar'daki `Sepetim` bağlantısını doğrula.
3. `Ürünler` bölümünden stoklu bir ürünün detayını aç.
4. Adet seçip `Sepete ekle` butonuna bas.
5. Sepette miktarı güncelle, ürünü çıkar ve sepeti temizle.
6. Employee hesabıyla `/cart` erişiminin reddedildiğini doğrula.

# 11. Beklenen Sonuç

```text
Solution Release build          : 0 uyarı, 0 hata
EF pending model changes        : yok
Migration                       : 20260716221538_AddShoppingCart
Kunde login                     : 204
Employee GET /api/cart          : 403
Sepete ekleme                   : 200
Stok üstü miktar                : 409
Miktar 2, toplam 39.98 test     : başarılı
Sepetten çıkarma                : 200
MVC add-to-cart yönlendirmesi   : /cart
Navbar Sepetim bağlantısı       : mevcut
Geçici test ürünü               : temizlendi
Customer sepet öğesi            : 0
```

# 12. Yaygın Hatalar

- `403 Forbidden`: Hesap `Kunde` rolünde değildir. Employee ve Admin sepet kullanamaz.
- `409 Conflict`: İstenen toplam miktar stoktan fazladır veya eşzamanlı sepet değişikliği oluşmuştur.
- `400 Bad Request`: Ürün aktif değildir, kategori pasiftir veya request doğrulaması başarısızdır.
- Navbar'da sepet görünmüyor: Yeni role claim'inin cookie'ye girmesi için çıkış yapıp tekrar giriş yap.
- `Invalid object name 'Carts'`: `dotnet ef database update` çalıştır.
- Pending model changes: Migration oluşturulduktan sonra projeyi build et ve database update komutunu yeniden çalıştır.
- MVC API'ye ulaşamıyor: API'nin `https://localhost:7001` adresinde çalıştığını ve MVC `ApiSettings:BaseUrl` değerini kontrol et.

# 13. Tamamlama Kontrol Listesi

```text
[x] CLI komutları doğru klasörde çalıştırıldı
[x] Cart ve CartItem doğru API projesinde oluşturuldu
[x] SQL migration üretildi ve uygulandı
[x] Web API hatasız derlendi
[x] MVC uygulaması hatasız derlendi
[x] MVC yalnızca HttpClient ile API'ye bağlandı
[x] MVC doğrudan veritabanına erişmiyor
[x] Kullanıcı kimliği API claim'inden alınıyor
[x] Fiyat ve stok API tarafından belirleniyor
[x] Kunde policy'si API'de uygulanıyor
[x] MVC POST formları antiforgery ile korunuyor
[x] Navbar'a Sepetim bağlantısı eklendi
[x] Ekleme, güncelleme, silme ve temizleme doğrulandı
[x] Stok sınırı ve Employee 403 sonucu doğrulandı
```
