# 1. Bu Adımın Amacı

Sepetteki güvenilir API verilerinden sipariş oluşturmak, ürün fiyatı ve toplamı sunucuda hesaplamak, stoğu atomik biçimde düşürmek ve müşterinin yalnızca kendi siparişlerini görebilmesini sağlamak.

# 2. Etkilenen Uygulama

```text
SecureShop.Api
SecureShop.Mvc
SQL Server
```

# 3. Bu Adımda Yapılacaklar

1. Sipariş ve sipariş kalemi domain modellerini eklemek.
2. Checkout sırasında serializable transaction kullanmak.
3. Fiyat, stok, kullanıcı ve toplamı yalnızca API'de belirlemek.
4. Sipariş anındaki ürün adı, SKU ve fiyatını snapshot olarak saklamak.
5. Sepeti başarılı siparişten sonra temizlemek.
6. Müşteri sipariş liste/detay ekranlarını eklemek.
7. IDOR'a karşı sipariş sorgusunu authenticated user kimliğiyle filtrelemek.

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
├── SecureShop.Api/Domain/{Entities,Enums}
├── SecureShop.Api/Data/{Configurations,Migrations}
├── SecureShop.Api/Features/Orders
├── SecureShop.Api/Controllers/OrdersController.cs
└── SecureShop.Mvc/{Controllers,Models,Services,Views/Orders}
tests/
```

# 6. Dosya Bazında Eksiksiz Kodlar

Bu bölümdeki içerikler tamamlanmış çalışma ağacındaki dosyaların eksiksiz güncel
halleridir; ``...`` ile kısaltma yapılmamıştır.

## `src/SecureShop.Api/Domain/Enums/OrderStatus.cs`

Uzantı: `.cs`

``csharp
namespace SecureShop.Api.Domain.Enums;

public enum OrderStatus
{
    PendingApproval = 1,
    Approved = 2,
    ReadyForPickup = 3,
    Completed = 4,
    Cancelled = 5
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

## `src/SecureShop.Api/Domain/Entities/OrderItem.cs`

Uzantı: `.cs`

``csharp
namespace SecureShop.Api.Domain.Entities;

public sealed class OrderItem
{
    private OrderItem()
    {
    }

    internal OrderItem(
        Guid orderId,
        Guid productId,
        string productName,
        string sku,
        decimal unitPrice,
        int quantity)
    {
        if (orderId == Guid.Empty || productId == Guid.Empty)
        {
            throw new ArgumentException(
                "Sipariş ve ürün kimliği boş olamaz.");
        }

        if (quantity is < 1 or > 99)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity));
        }

        if (unitPrice < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(unitPrice));
        }

        Id = Guid.NewGuid();
        OrderId = orderId;
        ProductId = productId;
        ProductName = Normalize(productName, 200, nameof(productName));
        Sku = Normalize(sku, 64, nameof(sku)).ToUpperInvariant();
        UnitPrice = decimal.Round(
            unitPrice,
            2,
            MidpointRounding.ToEven);
        Quantity = quantity;
        LineTotal = decimal.Round(
            UnitPrice * Quantity,
            2,
            MidpointRounding.ToEven);
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public Guid ProductId { get; private set; }

    public string ProductName { get; private set; } = string.Empty;

    public string Sku { get; private set; } = string.Empty;

    public decimal UnitPrice { get; private set; }

    public int Quantity { get; private set; }

    public decimal LineTotal { get; private set; }

    public Order Order { get; private set; } = null!;

    public Product Product { get; private set; } = null!;

    private static string Normalize(
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
            throw new ArgumentOutOfRangeException(parameterName);
        }

        return normalized;
    }
}
``

## `src/SecureShop.Api/Domain/Entities/Product.cs`

Uzantı: `.cs`

``csharp
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

    public void DecreaseStock(int quantity)
    {
        if (quantity < 1 || quantity > StockQuantity)
        {
            throw new InvalidOperationException(
                "Ürün stoğu sipariş miktarı için yetersiz.");
        }

        StockQuantity -= quantity;
        MarkAsUpdated();
    }

    public void IncreaseStock(int quantity)
    {
        if (quantity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        checked
        {
            StockQuantity += quantity;
        }

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
``

## `src/SecureShop.Api/Data/Configurations/OrderConfiguration.cs`

Uzantı: `.cs`

``csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecureShop.Api.Domain.Entities;
using SecureShop.Api.Domain.Enums;
using SecureShop.Api.Security.Identity;

namespace SecureShop.Api.Data.Configurations;

public sealed class OrderConfiguration
    : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders", table =>
        {
            table.HasCheckConstraint(
                "CK_Orders_TotalAmount_NonNegative",
                "[TotalAmount] >= 0");
            table.HasCheckConstraint(
                "CK_Orders_Status_Range",
                $"[Status] BETWEEN {(int)OrderStatus.PendingApproval} AND {(int)OrderStatus.Cancelled}");
        });

        builder.HasKey(order => order.Id);

        builder.Property(order => order.OrderNumber)
            .HasMaxLength(32)
            .IsUnicode(false)
            .IsRequired();

        builder.HasIndex(order => order.OrderNumber)
            .IsUnique()
            .HasDatabaseName("UX_Orders_OrderNumber");

        builder.HasIndex(order => new
            {
                order.UserId,
                order.CreatedAtUtc
            })
            .HasDatabaseName("IX_Orders_UserId_CreatedAtUtc");

        builder.Property(order => order.RecipientName)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(order => order.AddressLine)
            .HasMaxLength(500)
            .IsRequired();
        builder.Property(order => order.PostalCode)
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(order => order.City)
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(order => order.Country)
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(order => order.Status)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(order => order.TotalAmount)
            .HasPrecision(18, 2)
            .IsRequired();
        builder.Property(order => order.CreatedAtUtc)
            .HasPrecision(0)
            .IsRequired();
        builder.Property(order => order.UpdatedAtUtc)
            .HasPrecision(0);
        builder.Property(order => order.CompletedAtUtc)
            .HasPrecision(0);
        builder.Property(order => order.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasMany(order => order.Items)
            .WithOne(item => item.Order)
            .HasForeignKey(item => item.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(order => order.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(order => order.ProcessedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
``

## `src/SecureShop.Api/Data/Configurations/OrderItemConfiguration.cs`

Uzantı: `.cs`

``csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecureShop.Api.Domain.Entities;

namespace SecureShop.Api.Data.Configurations;

public sealed class OrderItemConfiguration
    : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems", table =>
        {
            table.HasCheckConstraint(
                "CK_OrderItems_Quantity_Range",
                "[Quantity] BETWEEN 1 AND 99");
            table.HasCheckConstraint(
                "CK_OrderItems_UnitPrice_NonNegative",
                "[UnitPrice] >= 0");
            table.HasCheckConstraint(
                "CK_OrderItems_LineTotal_NonNegative",
                "[LineTotal] >= 0");
        });

        builder.HasKey(item => item.Id);

        builder.HasIndex(item => new
            {
                item.OrderId,
                item.ProductId
            })
            .IsUnique()
            .HasDatabaseName("UX_OrderItems_OrderId_ProductId");

        builder.Property(item => item.ProductName)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(item => item.Sku)
            .HasMaxLength(64)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(item => item.UnitPrice)
            .HasPrecision(18, 2)
            .IsRequired();
        builder.Property(item => item.Quantity)
            .IsRequired();
        builder.Property(item => item.LineTotal)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.HasOne(item => item.Product)
            .WithMany()
            .HasForeignKey(item => item.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
``

## `src/SecureShop.Api/Data/AppDbContext.cs`

Uzantı: `.cs`

``csharp
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

    public DbSet<ProductImage> ProductImages => Set<ProductImage>();

    public DbSet<Cart> Carts => Set<Cart>();

    public DbSet<CartItem> CartItems => Set<CartItem>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AppDbContext).Assembly);
    }
}
``

## `src/SecureShop.Api/Data/Migrations/20260717212852_AddOrdersAndAudit.cs`

Uzantı: `.cs`

``csharp
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureShop.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrdersAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Action = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    DetailsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IpAddress = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderNumber = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    RecipientName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AddressLine = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PostalCode = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ProcessedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset(0)", precision: 0, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.CheckConstraint("CK_Orders_Status_Range", "[Status] BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_Orders_TotalAmount_NonNegative", "[TotalAmount] >= 0");
                    table.ForeignKey(
                        name: "FK_Orders_AspNetUsers_ProcessedByUserId",
                        column: x => x.ProcessedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Sku = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.Id);
                    table.CheckConstraint("CK_OrderItems_LineTotal_NonNegative", "[LineTotal] >= 0");
                    table.CheckConstraint("CK_OrderItems_Quantity_Range", "[Quantity] BETWEEN 1 AND 99");
                    table.CheckConstraint("CK_OrderItems_UnitPrice_NonNegative", "[UnitPrice] >= 0");
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CreatedAtUtc",
                table: "AuditLogs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Entity",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_ProductId",
                table: "OrderItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "UX_OrderItems_OrderId_ProductId",
                table: "OrderItems",
                columns: new[] { "OrderId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ProcessedByUserId",
                table: "Orders",
                column: "ProcessedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_UserId_CreatedAtUtc",
                table: "Orders",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_Orders_OrderNumber",
                table: "Orders",
                column: "OrderNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "OrderItems");

            migrationBuilder.DropTable(
                name: "Orders");
        }
    }
}
``

## `src/SecureShop.Api/Contracts/Requests/CreateOrderRequest.cs`

Uzantı: `.cs`

``csharp
using System.ComponentModel.DataAnnotations;

namespace SecureShop.Api.Contracts.Requests;

public sealed class CreateOrderRequest
{
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string RecipientName { get; init; } = string.Empty;

    [Required]
    [StringLength(500, MinimumLength = 5)]
    public string AddressLine { get; init; } = string.Empty;

    [Required]
    [StringLength(20, MinimumLength = 2)]
    public string PostalCode { get; init; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string City { get; init; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Country { get; init; } = string.Empty;
}
``

## `src/SecureShop.Api/Contracts/Responses/OrderItemResponse.cs`

Uzantı: `.cs`

``csharp
namespace SecureShop.Api.Contracts.Responses;

public sealed record OrderItemResponse(
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);
``

## `src/SecureShop.Api/Contracts/Responses/OrderResponse.cs`

Uzantı: `.cs`

``csharp
namespace SecureShop.Api.Contracts.Responses;

public sealed record OrderResponse(
    Guid Id,
    string OrderNumber,
    Guid UserId,
    string RecipientName,
    string AddressLine,
    string PostalCode,
    string City,
    string Country,
    string Status,
    decimal TotalAmount,
    IReadOnlyList<OrderItemResponse> Items,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string RowVersion,
    string? QrCodeDataUrl);
``

## `src/SecureShop.Api/Features/Orders/OrderMutationResult.cs`

Uzantı: `.cs`

``csharp
using SecureShop.Api.Contracts.Responses;

namespace SecureShop.Api.Features.Orders;

public enum OrderMutationStatus
{
    Succeeded,
    CartEmpty,
    ProductUnavailable,
    InsufficientStock,
    NotFound,
    Forbidden,
    InvalidTransition,
    InvalidRowVersion,
    InvalidQrCode,
    ConcurrencyConflict
}

public sealed record OrderMutationResult(
    OrderMutationStatus Status,
    OrderResponse? Order = null);
``

## `src/SecureShop.Api/Features/Orders/IOrderService.cs`

Uzantı: `.cs`

``csharp
using SecureShop.Api.Contracts.Requests;
using SecureShop.Api.Contracts.Responses;

namespace SecureShop.Api.Features.Orders;

public interface IOrderService
{
    Task<OrderMutationResult> CreateAsync(
        Guid userId,
        CreateOrderRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<OrderResponse>> GetCustomerOrdersAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<OrderResponse?> GetCustomerOrderAsync(
        Guid userId,
        string orderNumber,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<OrderResponse>> GetStaffOrdersAsync(
        CancellationToken cancellationToken);

    Task<OrderResponse?> GetStaffOrderAsync(
        string orderNumber,
        CancellationToken cancellationToken);

    Task<OrderMutationResult> ApproveAsync(
        string orderNumber,
        Guid staffUserId,
        string rowVersion,
        CancellationToken cancellationToken);

    Task<OrderMutationResult> MarkReadyAsync(
        string orderNumber,
        Guid staffUserId,
        string rowVersion,
        CancellationToken cancellationToken);

    Task<OrderMutationResult> CancelAsync(
        string orderNumber,
        Guid staffUserId,
        string rowVersion,
        CancellationToken cancellationToken);

    Task<OrderMutationResult> CompleteByQrAsync(
        string token,
        Guid staffUserId,
        CancellationToken cancellationToken);
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

## `src/SecureShop.Api/Controllers/OrdersController.cs`

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
[Route("api/orders")]
[Authorize(Policy = AppPolicies.CustomerOnly)]
public sealed class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly ICurrentUserService _currentUser;

    public OrdersController(
        IOrderService orderService,
        ICurrentUserService currentUser)
    {
        _orderService = orderService;
        _currentUser = currentUser;
    }

    [HttpPost]
    public async Task<ActionResult<OrderResponse>> Create(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var result = await _orderService.CreateAsync(
            userId,
            request,
            cancellationToken);

        return ToActionResult(result, created: true);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrderResponse>>> GetMine(
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        return Ok(await _orderService.GetCustomerOrdersAsync(
            userId,
            cancellationToken));
    }

    [HttpGet("{orderNumber}")]
    public async Task<ActionResult<OrderResponse>> GetMineByNumber(
        string orderNumber,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var order = await _orderService.GetCustomerOrderAsync(
            userId,
            orderNumber,
            cancellationToken);

        return order is null ? NotFound() : Ok(order);
    }

    private ActionResult<OrderResponse> ToActionResult(
        OrderMutationResult result,
        bool created)
    {
        if (result.Status == OrderMutationStatus.Succeeded
            && result.Order is not null)
        {
            return created
                ? CreatedAtAction(
                    nameof(GetMineByNumber),
                    new
                    {
                        orderNumber = result.Order.OrderNumber
                    },
                    result.Order)
                : Ok(result.Order);
        }

        return result.Status switch
        {
            OrderMutationStatus.CartEmpty =>
                BadRequest(Problem("Sepet boş.")),
            OrderMutationStatus.ProductUnavailable =>
                Conflict(Problem("Sepette satışa kapalı ürün var.")),
            OrderMutationStatus.InsufficientStock =>
                Conflict(Problem("Ürün stoğu sipariş için yetersiz.")),
            OrderMutationStatus.ConcurrencyConflict =>
                Conflict(Problem("Sepet veya stok değişti. Sayfayı yenileyin.")),
            _ => StatusCode(
                StatusCodes.Status500InternalServerError)
        };
    }

    private static ProblemDetails Problem(string detail) =>
        new() { Detail = detail };
}
``

## `src/SecureShop.Mvc/Models/Requests/CreateOrderRequest.cs`

Uzantı: `.cs`

``csharp
namespace SecureShop.Mvc.Models.Requests;

public sealed record CreateOrderRequest(
    string RecipientName,
    string AddressLine,
    string PostalCode,
    string City,
    string Country);
``

## `src/SecureShop.Mvc/Models/Responses/OrderItemResponse.cs`

Uzantı: `.cs`

``csharp
namespace SecureShop.Mvc.Models.Responses;

public sealed record OrderItemResponse(
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);
``

## `src/SecureShop.Mvc/Models/Responses/OrderResponse.cs`

Uzantı: `.cs`

``csharp
namespace SecureShop.Mvc.Models.Responses;

public sealed record OrderResponse(
    Guid Id,
    string OrderNumber,
    Guid UserId,
    string RecipientName,
    string AddressLine,
    string PostalCode,
    string City,
    string Country,
    string Status,
    decimal TotalAmount,
    IReadOnlyList<OrderItemResponse> Items,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string RowVersion,
    string? QrCodeDataUrl);
``

## `src/SecureShop.Mvc/Models/ViewModels/CheckoutViewModel.cs`

Uzantı: `.cs`

``csharp
using System.ComponentModel.DataAnnotations;
using SecureShop.Mvc.Models.Responses;

namespace SecureShop.Mvc.Models.ViewModels;

public sealed class CheckoutViewModel
{
    [Required]
    [StringLength(200, MinimumLength = 2)]
    [Display(Name = "Teslim alacak kişi")]
    public string RecipientName { get; set; } = string.Empty;

    [Required]
    [StringLength(500, MinimumLength = 5)]
    [Display(Name = "Adres")]
    public string AddressLine { get; set; } = string.Empty;

    [Required]
    [StringLength(20, MinimumLength = 2)]
    [Display(Name = "Posta kodu")]
    public string PostalCode { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 2)]
    [Display(Name = "Şehir")]
    public string City { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 2)]
    [Display(Name = "Ülke")]
    public string Country { get; set; } = "Almanya";

    public CartResponse? Cart { get; set; }
}
``

## `src/SecureShop.Mvc/Models/ViewModels/OrderListViewModel.cs`

Uzantı: `.cs`

``csharp
using SecureShop.Mvc.Models.Responses;

namespace SecureShop.Mvc.Models.ViewModels;

public sealed class OrderListViewModel
{
    public IReadOnlyList<OrderResponse> Orders { get; init; } = [];

    public string? ErrorMessage { get; init; }
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

## `src/SecureShop.Mvc/Controllers/OrdersController.cs`

Uzantı: `.cs`

``csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureShop.Mvc.Models.Requests;
using SecureShop.Mvc.Models.ViewModels;
using SecureShop.Mvc.Security;
using SecureShop.Mvc.Services.Interfaces;

namespace SecureShop.Mvc.Controllers;

[Authorize(Roles = AppRoles.Customer)]
[Route("orders")]
public sealed class OrdersController : Controller
{
    private readonly IOrderApiService _orderApiService;
    private readonly ICartApiService _cartApiService;

    public OrdersController(
        IOrderApiService orderApiService,
        ICartApiService cartApiService)
    {
        _orderApiService = orderApiService;
        _cartApiService = cartApiService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken)
    {
        var result = await _orderApiService.GetMineAsync(
            cancellationToken);

        return View(new OrderListViewModel
        {
            Orders = result.Data ?? [],
            ErrorMessage = result.ErrorMessage
        });
    }

    [HttpGet("checkout")]
    public async Task<IActionResult> Checkout(
        CancellationToken cancellationToken)
    {
        var cartResult = await _cartApiService.GetAsync(
            cancellationToken);

        if (!cartResult.IsSuccess
            || cartResult.Data is null
            || cartResult.Data.Items.Count == 0)
        {
            TempData["ErrorMessage"] =
                cartResult.ErrorMessage ?? "Sepetiniz boş.";

            return RedirectToAction("Index", "Cart");
        }

        return View(new CheckoutViewModel
        {
            Cart = cartResult.Data
        });
    }

    [HttpPost("checkout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(
        CheckoutViewModel model,
        CancellationToken cancellationToken)
    {
        var cartResult = await _cartApiService.GetAsync(
            cancellationToken);
        model.Cart = cartResult.Data;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _orderApiService.CreateAsync(
            new CreateOrderRequest(
                model.RecipientName.Trim(),
                model.AddressLine.Trim(),
                model.PostalCode.Trim(),
                model.City.Trim(),
                model.Country.Trim()),
            cancellationToken);

        if (!result.IsSuccess || result.Data is null)
        {
            ModelState.AddModelError(
                string.Empty,
                result.ErrorMessage
                    ?? "Sipariş oluşturulamadı.");

            return View(model);
        }

        TempData["SuccessMessage"] =
            "Siparişiniz oluşturuldu ve personel onayına gönderildi.";

        return RedirectToAction(
            nameof(Details),
            new
            {
                orderNumber = result.Data.OrderNumber
            });
    }

    [HttpGet("{orderNumber}")]
    public async Task<IActionResult> Details(
        string orderNumber,
        CancellationToken cancellationToken)
    {
        var result = await _orderApiService.GetMineAsync(
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
}
``

## `src/SecureShop.Mvc/Views/Orders/Index.cshtml`

Uzantı: `.cshtml`

``cshtml
@model SecureShop.Mvc.Models.ViewModels.OrderListViewModel
@{
    ViewData["Title"] = "Siparişlerim";
}

<div class="d-flex justify-content-between align-items-center mb-4">
    <div>
        <span class="text-uppercase text-primary fw-semibold small">Hesabım</span>
        <h1 class="mb-0">Siparişlerim</h1>
    </div>
    <a asp-controller="Products" asp-action="Index" class="btn btn-outline-primary">
        Alışverişe devam et
    </a>
</div>

@if (TempData["ErrorMessage"] is string operationError)
{
    <div class="alert alert-danger">@operationError</div>
}

@if (!string.IsNullOrWhiteSpace(Model.ErrorMessage))
{
    <div class="alert alert-danger">@Model.ErrorMessage</div>
}
else if (Model.Orders.Count == 0)
{
    <div class="card border-0 shadow-sm">
        <div class="card-body p-5 text-center">
            <h2 class="h4">Henüz siparişiniz yok</h2>
            <a asp-controller="Products" asp-action="Index" class="btn btn-primary">
                Ürünleri keşfet
            </a>
        </div>
    </div>
}
else
{
    <div class="row g-3">
        @foreach (var order in Model.Orders)
        {
            <div class="col-12">
                <article class="card border-0 shadow-sm order-card">
                    <div class="card-body p-4 d-flex flex-wrap justify-content-between gap-3">
                        <div>
                            <span class="text-body-secondary small">
                                @order.CreatedAtUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm")
                            </span>
                            <h2 class="h5 mt-1 mb-2">@order.OrderNumber</h2>
                            <span class="badge text-bg-primary">@order.Status</span>
                        </div>
                        <div class="text-end">
                            <strong class="d-block fs-5">@order.TotalAmount.ToString("N2") €</strong>
                            <span class="text-body-secondary small">@order.Items.Count ürün satırı</span>
                            <div class="mt-2">
                                <a asp-action="Details"
                                   asp-route-orderNumber="@order.OrderNumber"
                                   class="btn btn-sm btn-outline-primary">
                                    Detay
                                </a>
                            </div>
                        </div>
                    </div>
                </article>
            </div>
        }
    </div>
}
``

## `src/SecureShop.Mvc/Views/Orders/Checkout.cshtml`

Uzantı: `.cshtml`

``cshtml
@model SecureShop.Mvc.Models.ViewModels.CheckoutViewModel
@{
    ViewData["Title"] = "Siparişi tamamla";
}

<div class="row g-4">
    <div class="col-lg-7">
        <div class="card border-0 shadow-sm">
            <div class="card-body p-4 p-lg-5">
                <span class="text-uppercase text-primary fw-semibold small">Güvenli checkout</span>
                <h1 class="mt-2 mb-4">Teslimat bilgileri</h1>
                <form asp-action="Checkout" method="post">
                    <div asp-validation-summary="ModelOnly" class="alert alert-danger"></div>
                    <div class="row g-3">
                        <div class="col-12">
                            <label asp-for="RecipientName" class="form-label"></label>
                            <input asp-for="RecipientName" class="form-control" autocomplete="name" />
                            <span asp-validation-for="RecipientName" class="text-danger"></span>
                        </div>
                        <div class="col-12">
                            <label asp-for="AddressLine" class="form-label"></label>
                            <textarea asp-for="AddressLine" class="form-control" rows="3" autocomplete="street-address"></textarea>
                            <span asp-validation-for="AddressLine" class="text-danger"></span>
                        </div>
                        <div class="col-md-4">
                            <label asp-for="PostalCode" class="form-label"></label>
                            <input asp-for="PostalCode" class="form-control" autocomplete="postal-code" />
                            <span asp-validation-for="PostalCode" class="text-danger"></span>
                        </div>
                        <div class="col-md-4">
                            <label asp-for="City" class="form-label"></label>
                            <input asp-for="City" class="form-control" autocomplete="address-level2" />
                            <span asp-validation-for="City" class="text-danger"></span>
                        </div>
                        <div class="col-md-4">
                            <label asp-for="Country" class="form-label"></label>
                            <input asp-for="Country" class="form-control" autocomplete="country-name" />
                            <span asp-validation-for="Country" class="text-danger"></span>
                        </div>
                    </div>
                    <button type="submit" class="btn btn-primary btn-lg mt-4">
                        Siparişi oluştur
                    </button>
                </form>
            </div>
        </div>
    </div>
    <div class="col-lg-5">
        <div class="card border-0 shadow-sm sticky-lg-top order-summary-card">
            <div class="card-body p-4">
                <h2 class="h5">Sipariş özeti</h2>
                @if (Model.Cart is not null)
                {
                    @foreach (var item in Model.Cart.Items)
                    {
                        <div class="d-flex justify-content-between gap-3 py-2 border-bottom">
                            <span>@item.ProductName × @item.Quantity</span>
                            <strong>@item.LineTotal.ToString("N2") €</strong>
                        </div>
                    }
                    <div class="d-flex justify-content-between mt-3 fs-5">
                        <strong>Toplam</strong>
                        <strong>@Model.Cart.TotalAmount.ToString("N2") €</strong>
                    </div>
                    <p class="small text-body-secondary mt-3 mb-0">
                        Fiyat ve stok sipariş oluşturulurken API tarafından yeniden doğrulanır.
                    </p>
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

## `src/SecureShop.Mvc/Views/Cart/Index.cshtml`

Uzantı: `.cshtml`

``cshtml
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
                    <a asp-controller="Orders"
                       asp-action="Checkout"
                       class="btn btn-primary w-100">
                        Siparişi tamamla
                    </a>
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
``

# 7. Kod Açıklaması

`OrderService` sepeti ürün ve kategoriyle birlikte yükler, bütün uygunluk kontrollerini API'de yapar ve ilişkisel veritabanında serializable transaction açar. `Product.DecreaseStock` yetersiz stoğu reddeder. `OrderItem` fiyat snapshot'ı sayesinde ürün fiyatı sonradan değişse bile sipariş geçmişi değişmez. Customer endpoint'leri `ICurrentUserService.UserId` filtresi kullandığı için URL değiştirerek başka müşterinin siparişine erişilemez.

# 8. API–MVC Veri Akışı

```text
Razor Checkout View
    ↓
MVC OrdersController
    ↓
IOrderApiService / HttpClient
    ↓
POST /api/orders
    ↓
OrderService + authenticated UserId
    ↓
Serializable transaction
    ↓
Products + Cart + Orders + OrderItems
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

Checkout sonrası `PendingApproval` siparişi oluşur, stok seçilen miktar kadar azalır, sepet boşalır ve müşteri siparişi numarasıyla görebilir. MVC'nin gönderdiği payload kullanıcı kimliği, birim fiyat veya toplam tutar içermez.

# 12. Yaygın Hatalar

- Sepet boşsa API `400` döndürür.
- Ürün pasif veya kategori kapalıysa `409` döner.
- Stok yetmezse transaction sipariş oluşturmaz ve sepeti değiştirmez.
- Başka müşterinin sipariş numarası `404` olarak görünür.

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