# 1. Bu Adımın Amacı

Sepetteki ürünleri ana fotoğraflarıyla göstermek; adet alanı, artı ve eksi
düğmeleri değiştiğinde sayfayı yenilemeden güvenli biçimde API üzerinden sepeti
güncellemek; satır ara toplamı, farklı ürün sayısı, toplam ürün adedi, genel
toplam ve navbar sepet rozetini aynı response ile senkronize etmek.

# 2. Etkilenen Uygulama

```text
SecureShop.Api
SecureShop.Mvc
Test projeleri
```

SQL Server şeması değişmediği için migration gerekmez.

# 3. Bu Adımda Yapılacaklar

1. Cart response'a ana ürün fotoğrafı ve alt metni eklemek.
2. Cart sorgusunda ProductImages koleksiyonunu yüklemek.
3. MVC update action'ından AJAX istekleri için JSON cart response döndürmek.
4. Sepet satırlarına ürün fotoğrafı, stok durumu ve modern adet kontrolü eklemek.
5. Adet değişikliğini debounce ederek CSRF token ile MVC'ye göndermek.
6. Ara toplam ve bütün sepet özetlerini API response'undan güncellemek.
7. Navbar üzerinde sunucu tarafından hesaplanan toplam adet rozetini göstermek.
8. JavaScript kapalıysa klasik form submit/redirect davranışını korumak.
9. API ve MVC servis testlerini eklemek.

# 4. CLI Komutları

Çalışma dizini: `D:\Code\ASP.NET\SecureShop`

```powershell
dotnet restore SecureShop.sln
dotnet build SecureShop.sln -c Release --no-restore
dotnet test SecureShop.sln -c Release --no-build
node --check src\SecureShop.Mvc\wwwroot\js\cart.js
```

Terminal 1 — `src\SecureShop.Api`

```powershell
dotnet watch run --launch-profile https
```

Terminal 2 — `src\SecureShop.Mvc`

```powershell
dotnet watch run --launch-profile https
```

# 5. Güncel Proje Yapısı

```text
src/
├── SecureShop.Api/
│   ├── Contracts/Responses/CartItemResponse.cs
│   └── Features/Cart/CartService.cs
└── SecureShop.Mvc/
    ├── Controllers/CartController.cs
    ├── Models/
    ├── ViewComponents/CartNavigationViewComponent.cs
    ├── Views/Cart/Index.cshtml
    ├── Views/Shared/Components/CartNavigation/Default.cshtml
    └── wwwroot/
        ├── css/site.css
        └── js/cart.js
tests/
├── SecureShop.Api.UnitTests/CartServiceTests.cs
└── SecureShop.Mvc.Tests/CartApiServiceTests.cs
```

# 6. Dosya Bazında Eksiksiz Kodlar

Bu bölümde ilgili dosyaların tamamlanmış çalışma ağacındaki eksiksiz güncel
içerikleri bulunur.

## `src/SecureShop.Api/Contracts/Responses/CartItemResponse.cs`

Uzantı: `.cs`

```csharp
namespace SecureShop.Api.Contracts.Responses;

public sealed record CartItemResponse(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string Sku,
    string? ImageUrl,
    string ImageAltText,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal,
    int AvailableStock,
    bool IsAvailable);
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
            .Include(cart => cart.Items)
                .ThenInclude(item => item.Product)
                    .ThenInclude(product => product.Images)
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
                var image = item.Product.Images
                    .OrderByDescending(currentImage =>
                        currentImage.IsPrimary)
                    .ThenBy(currentImage =>
                        currentImage.SortOrder)
                    .FirstOrDefault();
                var lineTotal = decimal.Round(
                    item.Product.Price * item.Quantity,
                    2,
                    MidpointRounding.ToEven);

                return new CartItemResponse(
                    item.Id,
                    item.ProductId,
                    item.Product.Name,
                    item.Product.Sku,
                    image?.ImageUrl,
                    image?.AltText ?? item.Product.Name,
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

## `src/SecureShop.Mvc/Models/Responses/CartItemResponse.cs`

Uzantı: `.cs`

```csharp
namespace SecureShop.Mvc.Models.Responses;

public sealed record CartItemResponse(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string Sku,
    string? ImageUrl,
    string ImageAltText,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal,
    int AvailableStock,
    bool IsAvailable);
```

## `src/SecureShop.Mvc/Models/ViewModels/CartNavigationViewModel.cs`

Uzantı: `.cs`

```csharp
namespace SecureShop.Mvc.Models.ViewModels;

public sealed record CartNavigationViewModel(
    int TotalQuantity);
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
            if (WantsJsonResponse())
            {
                return BadRequest(new
                {
                    error = "Adet 1 ile 99 arasında olmalıdır."
                });
            }

            TempData["ErrorMessage"] =
                "Adet 1 ile 99 arasında olmalıdır.";

            return RedirectToAction(nameof(Index));
        }

        var result = await _cartApiService.UpdateItemAsync(
            itemId,
            request,
            cancellationToken);

        if (WantsJsonResponse())
        {
            if (result.IsSuccess && result.Data is not null)
            {
                return Ok(result.Data);
            }

            return StatusCode(
                (int)result.StatusCode,
                new
                {
                    error = result.ErrorMessage
                        ?? "Sepet miktarı güncellenemedi."
                });
        }

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

    private bool WantsJsonResponse() =>
        Request.Headers.Accept.Any(value =>
            value?.Contains(
                "application/json",
                StringComparison.OrdinalIgnoreCase) == true)
        || string.Equals(
            Request.Headers.XRequestedWith,
            "XMLHttpRequest",
            StringComparison.OrdinalIgnoreCase);
}
```

## `src/SecureShop.Mvc/ViewComponents/CartNavigationViewComponent.cs`

Uzantı: `.cs`

```csharp
using Microsoft.AspNetCore.Mvc;
using SecureShop.Mvc.Models.ViewModels;
using SecureShop.Mvc.Services.Interfaces;

namespace SecureShop.Mvc.ViewComponents;

public sealed class CartNavigationViewComponent
    : ViewComponent
{
    private readonly ICartApiService _cartApiService;

    public CartNavigationViewComponent(
        ICartApiService cartApiService)
    {
        _cartApiService = cartApiService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var result = await _cartApiService.GetAsync(
            HttpContext.RequestAborted);

        return View(new CartNavigationViewModel(
            result.Data?.TotalQuantity ?? 0));
    }
}
```

## `src/SecureShop.Mvc/Views/Shared/Components/CartNavigation/Default.cshtml`

Uzantı: `.cshtml`

```cshtml
@model SecureShop.Mvc.Models.ViewModels.CartNavigationViewModel

<li class="nav-item">
    <a class="nav-link nav-cart-link"
       asp-controller="Cart"
       asp-action="Index"
       aria-label="Sepetim, @Model.TotalQuantity ürün">
        <span class="nav-cart-icon" aria-hidden="true">🛒</span>
        <span>Sepetim</span>
        <span class="nav-cart-count"
              data-cart-nav-count
              aria-live="polite">@Model.TotalQuantity</span>
    </a>
</li>
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
                        @if (User.IsInRole(AppRoles.Customer))
                        {
                            @await Component.InvokeAsync("CartNavigation")
                        }
                        else if (User.Identity?.IsAuthenticated != true)
                        {
                            <li class="nav-item">
                                <a class="nav-link nav-cart-link"
                                   asp-controller="Cart"
                                   asp-action="Index"
                                   aria-label="Sepetim">
                                    <span class="nav-cart-icon" aria-hidden="true">🛒</span>
                                    <span>Sepetim</span>
                                </a>
                            </li>
                        }
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

## `src/SecureShop.Mvc/Views/Cart/Index.cshtml`

Uzantı: `.cshtml`

```cshtml
@model SecureShop.Mvc.Models.ViewModels.CartViewModel
@{
    ViewData["Title"] = "Sepetim";
    var cart = Model.Cart;
}

<div class="cart-page" data-cart-page>
    <div class="d-flex flex-wrap justify-content-between align-items-center gap-3 mb-4">
        <div>
            <span class="text-uppercase text-primary fw-semibold small">Secure checkout</span>
            <div class="d-flex align-items-center gap-3 mt-1">
                <h1 class="mb-0">Sepetim</h1>
                @if (cart is not null && cart.Items.Count > 0)
                {
                    <span class="cart-heading-count">
                        <strong data-cart-heading-quantity>@cart.TotalQuantity</strong> ürün
                    </span>
                }
            </div>
        </div>
        <a asp-controller="Products" asp-action="Index" class="btn btn-outline-primary">
            Alışverişe devam et
        </a>
    </div>

    <div class="cart-live-status visually-hidden"
         data-cart-live-status
         role="status"
         aria-live="polite"></div>

    <div class="alert alert-danger d-none"
         data-cart-error
         role="alert"></div>

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
        <div class="row g-4 align-items-start">
            <div class="col-lg-8">
                <div class="cart-list-header mb-3">
                    <span>@cart.Items.Count farklı ürün</span>
                    <span>Fiyat</span>
                </div>

                <div class="vstack gap-3">
                    @foreach (var item in cart.Items)
                    {
                        <article class="card border-0 shadow-sm cart-item-card"
                                 data-cart-item
                                 data-item-id="@item.Id">
                            <div class="card-body p-3 p-md-4">
                                <div class="cart-item-grid">
                                    <a asp-controller="Products"
                                       asp-action="Details"
                                       asp-route-sku="@item.Sku"
                                       class="cart-item-image-link">
                                        @if (!string.IsNullOrWhiteSpace(item.ImageUrl))
                                        {
                                            <img src="@item.ImageUrl"
                                                 alt="@item.ImageAltText"
                                                 class="cart-item-image" />
                                        }
                                        else
                                        {
                                            <span class="cart-item-image-placeholder" aria-hidden="true">📦</span>
                                        }
                                    </a>

                                    <div class="cart-item-content">
                                        <a asp-controller="Products"
                                           asp-action="Details"
                                           asp-route-sku="@item.Sku"
                                           class="cart-item-title">
                                            @item.ProductName
                                        </a>
                                        <div class="text-body-secondary small mt-1">SKU: @item.Sku</div>
                                        <div class="cart-stock-status @(item.IsAvailable ? "is-available" : "is-unavailable")">
                                            @(item.IsAvailable
                                                ? $"Stokta · {item.AvailableStock} adet mevcut"
                                                : "Bu ürün şu anda satışa uygun değil")
                                        </div>
                                        <div class="small text-body-secondary mt-2">
                                            Birim fiyat:
                                            <strong class="text-body">@item.UnitPrice.ToString("N2") €</strong>
                                        </div>

                                        <div class="d-flex flex-wrap align-items-center gap-3 mt-3">
                                            <form asp-action="Update"
                                                  asp-route-itemId="@item.Id"
                                                  method="post"
                                                  class="cart-quantity-form"
                                                  data-cart-quantity-form>
                                                <span class="visually-hidden" id="quantity-label-@item.Id">
                                                    @item.ProductName adedi
                                                </span>
                                                <button type="button"
                                                        class="cart-quantity-button"
                                                        data-quantity-decrement
                                                        aria-label="@item.ProductName adetini azalt"
                                                        disabled="@(!item.IsAvailable || item.Quantity <= 1)">−</button>
                                                <input id="quantity-@item.Id"
                                                       name="Quantity"
                                                       type="number"
                                                       inputmode="numeric"
                                                       min="1"
                                                       max="@Math.Min(item.AvailableStock, 99)"
                                                       value="@item.Quantity"
                                                       data-cart-quantity
                                                       data-committed-value="@item.Quantity"
                                                       aria-labelledby="quantity-label-@item.Id"
                                                       disabled="@(!item.IsAvailable)" />
                                                <button type="button"
                                                        class="cart-quantity-button"
                                                        data-quantity-increment
                                                        aria-label="@item.ProductName adetini artır"
                                                        disabled="@(!item.IsAvailable || item.Quantity >= Math.Min(item.AvailableStock, 99))">+</button>
                                                <span class="cart-quantity-spinner spinner-border spinner-border-sm"
                                                      data-cart-spinner
                                                      aria-hidden="true"></span>
                                            </form>

                                            <form asp-action="Remove"
                                                  asp-route-itemId="@item.Id"
                                                  method="post">
                                                <button type="submit"
                                                        class="btn btn-sm btn-link text-danger text-decoration-none p-0">
                                                    Sepetten çıkar
                                                </button>
                                            </form>
                                        </div>
                                    </div>

                                    <div class="cart-item-price">
                                        <span class="small text-body-secondary">Ara toplam</span>
                                        <strong data-line-total>@item.LineTotal.ToString("N2") €</strong>
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
                        <h2 class="h4 mb-4">Sipariş özeti</h2>
                        <div class="d-flex justify-content-between py-2">
                            <span>Farklı ürün</span>
                            <strong data-cart-distinct-count>@cart.Items.Count</strong>
                        </div>
                        <div class="d-flex justify-content-between py-2 border-bottom">
                            <span>Toplam ürün adedi</span>
                            <strong data-cart-total-quantity>@cart.TotalQuantity</strong>
                        </div>
                        <div class="d-flex justify-content-between py-3">
                            <span>Ara toplam</span>
                            <strong data-cart-subtotal>@cart.TotalAmount.ToString("N2") €</strong>
                        </div>
                        <div class="d-flex justify-content-between py-2 border-top cart-grand-total">
                            <span>Toplam</span>
                            <strong data-cart-total>@cart.TotalAmount.ToString("N2") €</strong>
                        </div>
                        <p class="small text-body-secondary mt-3">
                            Nihai fiyat ve stok sipariş oluşturulurken API tarafından tekrar doğrulanır.
                        </p>
                        <a asp-controller="Orders"
                           asp-action="Checkout"
                           class="btn btn-primary btn-lg w-100">
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
</div>

@section Scripts {
    <script src="~/js/cart.js" asp-append-version="true"></script>
}
```

## `src/SecureShop.Mvc/wwwroot/js/cart.js`

Uzantı: `.js`

```javascript
(() => {
    "use strict";

    const page = document.querySelector("[data-cart-page]");

    if (!page) {
        return;
    }

    const currencyFormatter = new Intl.NumberFormat("tr-TR", {
        style: "currency",
        currency: "EUR"
    });
    const errorBox = page.querySelector("[data-cart-error]");
    const liveStatus = page.querySelector("[data-cart-live-status]");
    const pendingUpdates = new WeakMap();

    const formatCurrency = (value) =>
        currencyFormatter.format(Number(value));

    const announce = (message) => {
        if (!liveStatus) {
            return;
        }

        liveStatus.textContent = "";
        window.setTimeout(() => {
            liveStatus.textContent = message;
        }, 30);
    };

    const showError = (message) => {
        if (!errorBox) {
            return;
        }

        errorBox.textContent = message;
        errorBox.classList.remove("d-none");
        announce(message);
    };

    const clearError = () => {
        if (!errorBox) {
            return;
        }

        errorBox.textContent = "";
        errorBox.classList.add("d-none");
    };

    const setFormBusy = (form, isBusy) => {
        form.classList.toggle("is-updating", isBusy);
        form.setAttribute("aria-busy", isBusy ? "true" : "false");

        form.querySelectorAll("button, input").forEach((control) => {
            if (isBusy) {
                control.dataset.wasDisabled = control.disabled ? "true" : "false";
                control.disabled = true;
            } else {
                control.disabled = control.dataset.wasDisabled === "true";
                delete control.dataset.wasDisabled;
            }
        });
    };

    const updateButtons = (form) => {
        const input = form.querySelector("[data-cart-quantity]");
        const decrement = form.querySelector("[data-quantity-decrement]");
        const increment = form.querySelector("[data-quantity-increment]");

        if (!input || !decrement || !increment || form.classList.contains("is-updating")) {
            return;
        }

        const value = Number(input.value);
        const minimum = Number(input.min || 1);
        const maximum = Number(input.max || 99);

        decrement.disabled = value <= minimum;
        increment.disabled = value >= maximum;
    };

    const applyCart = (cart) => {
        cart.items.forEach((item) => {
            const card = page.querySelector(
                `[data-cart-item][data-item-id="${item.id}"]`);

            if (!card) {
                return;
            }

            const input = card.querySelector("[data-cart-quantity]");
            const lineTotal = card.querySelector("[data-line-total]");

            if (input) {
                input.value = item.quantity;
                input.dataset.committedValue = item.quantity;
            }

            if (lineTotal) {
                lineTotal.textContent = formatCurrency(item.lineTotal);
            }

            const form = card.querySelector("[data-cart-quantity-form]");
            if (form) {
                updateButtons(form);
            }
        });

        page.querySelectorAll("[data-cart-total-quantity], [data-cart-heading-quantity]")
            .forEach((element) => {
                element.textContent = cart.totalQuantity;
            });
        page.querySelectorAll("[data-cart-subtotal], [data-cart-total]")
            .forEach((element) => {
                element.textContent = formatCurrency(cart.totalAmount);
            });
        document.querySelectorAll("[data-cart-nav-count]")
            .forEach((element) => {
                element.textContent = cart.totalQuantity;
                const link = element.closest("a");
                if (link) {
                    link.setAttribute(
                        "aria-label",
                        `Sepetim, ${cart.totalQuantity} ürün`);
                }
            });
    };

    const readError = async (response) => {
        try {
            const payload = await response.json();
            return payload.error
                ?? payload.detail
                ?? "Sepet güncellenemedi.";
        } catch {
            return "Sepet güncellenemedi.";
        }
    };

    const updateQuantity = async (form) => {
        const input = form.querySelector("[data-cart-quantity]");

        if (!input || form.classList.contains("is-updating")) {
            return;
        }

        const minimum = Number(input.min || 1);
        const maximum = Number(input.max || 99);
        const requestedQuantity = Number(input.value);
        const committedQuantity = Number(input.dataset.committedValue);

        if (!Number.isInteger(requestedQuantity)
            || requestedQuantity < minimum
            || requestedQuantity > maximum) {
            input.value = committedQuantity;
            updateButtons(form);
            showError(`Adet ${minimum} ile ${maximum} arasında olmalıdır.`);
            return;
        }

        if (requestedQuantity === committedQuantity) {
            updateButtons(form);
            return;
        }

        clearError();
        const formData = new FormData(form);
        setFormBusy(form, true);

        try {
            const response = await fetch(form.action, {
                method: "POST",
                body: formData,
                headers: {
                    "Accept": "application/json",
                    "X-Requested-With": "XMLHttpRequest"
                },
                credentials: "same-origin"
            });

            if (!response.ok) {
                throw new Error(await readError(response));
            }

            const cart = await response.json();
            applyCart(cart);
            announce(`Ürün adedi ${requestedQuantity} olarak güncellendi.`);
        } catch (error) {
            input.value = committedQuantity;
            showError(
                error instanceof Error
                    ? error.message
                    : "Sepet güncellenemedi.");
        } finally {
            setFormBusy(form, false);
            updateButtons(form);
        }
    };

    const scheduleUpdate = (form, delay = 400) => {
        const currentTimer = pendingUpdates.get(form);
        if (currentTimer) {
            window.clearTimeout(currentTimer);
        }

        pendingUpdates.set(
            form,
            window.setTimeout(() => {
                pendingUpdates.delete(form);
                void updateQuantity(form);
            }, delay));
    };

    page.querySelectorAll("[data-cart-quantity-form]").forEach((form) => {
        const input = form.querySelector("[data-cart-quantity]");
        const decrement = form.querySelector("[data-quantity-decrement]");
        const increment = form.querySelector("[data-quantity-increment]");

        if (!input || !decrement || !increment) {
            return;
        }

        form.addEventListener("submit", (event) => {
            event.preventDefault();
            scheduleUpdate(form, 0);
        });

        input.addEventListener("input", () => {
            updateButtons(form);
            scheduleUpdate(form);
        });

        input.addEventListener("change", () => {
            scheduleUpdate(form, 0);
        });

        decrement.addEventListener("click", () => {
            input.stepDown();
            updateButtons(form);
            scheduleUpdate(form, 0);
        });

        increment.addEventListener("click", () => {
            input.stepUp();
            updateButtons(form);
            scheduleUpdate(form, 0);
        });

        updateButtons(form);
    });
})();
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
.nav-cart-link { display: inline-flex; align-items: center; gap: .4rem; }
.nav-cart-icon { font-size: 1.05rem; }
.nav-cart-count {
  display: inline-grid;
  min-width: 1.55rem;
  height: 1.55rem;
  place-items: center;
  padding: 0 .35rem;
  border-radius: 999px;
  background: #fbbf24;
  color: #172033;
  font-size: .75rem;
  font-weight: 800;
  line-height: 1;
}
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

.cart-item-card, .cart-summary-card, .cart-empty-state { border-radius: 1rem; }
.cart-item-card { overflow: hidden; transition: transform .16s ease, box-shadow .16s ease; }
.cart-item-card:hover { transform: translateY(-1px); }
.cart-summary-card { position: sticky; top: 1.5rem; }
.cart-empty-icon { font-size: 3rem; }
.cart-heading-count {
  padding: .35rem .7rem;
  border-radius: 999px;
  background: #e0e7ff;
  color: #3730a3;
  font-size: .85rem;
}
.cart-list-header {
  display: flex;
  justify-content: space-between;
  padding: 0 .25rem;
  color: #64748b;
  font-size: .8rem;
  font-weight: 700;
  letter-spacing: .04em;
  text-transform: uppercase;
}
.cart-item-grid {
  display: grid;
  grid-template-columns: 8.5rem minmax(0, 1fr) auto;
  gap: 1.25rem;
  align-items: center;
}
.cart-item-image-link {
  display: grid;
  width: 8.5rem;
  height: 8.5rem;
  place-items: center;
  overflow: hidden;
  border: 1px solid #e2e8f0;
  border-radius: .9rem;
  background: #fff;
}
.cart-item-image {
  width: 100%;
  height: 100%;
  padding: .5rem;
  object-fit: contain;
  transition: transform .18s ease;
}
.cart-item-image-link:hover .cart-item-image { transform: scale(1.04); }
.cart-item-image-placeholder { font-size: 2.5rem; }
.cart-item-title {
  color: #172033;
  font-size: 1.05rem;
  font-weight: 750;
  line-height: 1.35;
  text-decoration: none;
}
.cart-item-title:hover { color: #1d4ed8; }
.cart-stock-status {
  margin-top: .55rem;
  font-size: .8rem;
  font-weight: 650;
}
.cart-stock-status.is-available { color: #15803d; }
.cart-stock-status.is-unavailable { color: #b91c1c; }
.cart-item-price {
  min-width: 7rem;
  align-self: start;
  text-align: right;
}
.cart-item-price strong {
  display: block;
  margin-top: .25rem;
  color: #0f172a;
  font-size: 1.1rem;
}
.cart-quantity-form {
  display: inline-flex;
  min-height: 2.4rem;
  align-items: center;
  overflow: hidden;
  border: 1px solid #cbd5e1;
  border-radius: 999px;
  background: #fff;
  transition: border-color .15s ease, box-shadow .15s ease, opacity .15s ease;
}
.cart-quantity-form:focus-within {
  border-color: #2563eb;
  box-shadow: 0 0 0 .2rem rgba(37, 99, 235, .13);
}
.cart-quantity-form.is-updating { opacity: .68; }
.cart-quantity-form input {
  width: 3rem;
  border: 0;
  outline: 0;
  background: transparent;
  font-weight: 750;
  text-align: center;
  appearance: textfield;
  -moz-appearance: textfield;
}
.cart-quantity-form input::-webkit-inner-spin-button,
.cart-quantity-form input::-webkit-outer-spin-button { margin: 0; appearance: none; }
.cart-quantity-button {
  width: 2.35rem;
  align-self: stretch;
  border: 0;
  background: #f8fafc;
  color: #1e3a8a;
  font-size: 1.15rem;
  font-weight: 800;
  transition: background .12s ease;
}
.cart-quantity-button:hover:not(:disabled) { background: #dbeafe; }
.cart-quantity-button:disabled { color: #94a3b8; cursor: not-allowed; }
.cart-quantity-spinner {
  display: none;
  width: .85rem;
  height: .85rem;
  margin-right: .7rem;
}
.cart-quantity-form.is-updating .cart-quantity-spinner { display: inline-block; }
.cart-grand-total {
  align-items: center;
  font-size: 1.1rem;
}
.cart-grand-total strong {
  color: #0f172a;
  font-size: 1.45rem;
}

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
  .cart-item-grid { grid-template-columns: 6.5rem minmax(0, 1fr); gap: 1rem; align-items: start; }
  .cart-item-image-link { width: 6.5rem; height: 6.5rem; }
  .cart-item-price { grid-column: 2; min-width: 0; text-align: left; }
  .cart-list-header { display: none; }
  .product-gallery { grid-template-columns: 1fr; }
  .product-gallery-thumbnails { order: 2; flex-direction: row; overflow-x: auto; padding-bottom: .4rem; }
  .product-gallery-stage { min-height: 22rem; }
  .product-gallery-main-image { height: 22rem; }
}

@media (max-width: 575.98px) {
  .cart-item-grid { grid-template-columns: 1fr; }
  .cart-item-image-link { width: 100%; height: 13rem; }
  .cart-item-price { grid-column: auto; }
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

## `tests/SecureShop.Api.UnitTests/CartServiceTests.cs`

Uzantı: `.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using SecureShop.Api.Data;
using SecureShop.Api.Domain.Entities;
using SecureShop.Api.Features.Cart;

namespace SecureShop.Api.UnitTests;

public sealed class CartServiceTests
{
    [Fact]
    public async Task GetAsync_MapsPrimaryImageAndTrustedTotals()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var category = new Category("Elektronik");
        var product = new Product(
            category.Id,
            "Test Camera",
            "TEST-CAMERA-01",
            24.95m,
            10);
        product.AddImage(
            "/images/products/TEST-CAMERA-01/main.png",
            "Test kamera",
            0,
            isPrimary: true);
        var cart = new Cart(userId);
        cart.AddItem(product.Id, 2);

        dbContext.AddRange(category, product, cart);
        await dbContext.SaveChangesAsync();

        var result = await new CartService(dbContext)
            .GetAsync(userId, CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal(2, result.TotalQuantity);
        Assert.Equal(49.90m, result.TotalAmount);
        Assert.Equal(
            "/images/products/TEST-CAMERA-01/main.png",
            item.ImageUrl);
        Assert.Equal("Test kamera", item.ImageAltText);
    }

    [Fact]
    public async Task UpdateItemAsync_RecalculatesLineAndCartTotals()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var category = new Category("Elektronik");
        var product = new Product(
            category.Id,
            "Test Product",
            "TEST-PRODUCT-01",
            12.50m,
            10);
        var cart = new Cart(userId);
        var cartItem = cart.AddItem(product.Id, 1);

        dbContext.AddRange(category, product, cart);
        await dbContext.SaveChangesAsync();

        var result = await new CartService(dbContext)
            .UpdateItemAsync(
                userId,
                cartItem.Id,
                4,
                CancellationToken.None);

        Assert.Equal(
            CartMutationStatus.Succeeded,
            result.Status);
        Assert.NotNull(result.Cart);
        Assert.Equal(4, result.Cart.TotalQuantity);
        Assert.Equal(50m, result.Cart.TotalAmount);
        Assert.Equal(50m, Assert.Single(result.Cart.Items).LineTotal);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(
                $"SecureShop-CartService-{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }
}
```

## `tests/SecureShop.Mvc.Tests/CartApiServiceTests.cs`

Uzantı: `.cs`

```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SecureShop.Mvc.Models.Requests;
using SecureShop.Mvc.Models.Responses;
using SecureShop.Mvc.Services.Api;

namespace SecureShop.Mvc.Tests;

public sealed class CartApiServiceTests
{
    [Fact]
    public async Task UpdateItemAsync_SendsQuantityAndMapsNewTotals()
    {
        var itemId = Guid.NewGuid();
        HttpMethod? requestedMethod = null;
        string? requestedPath = null;
        UpdateCartItemQuantityRequest? requestPayload = null;
        var responseCart = new CartResponse(
            Guid.NewGuid(),
            [
                new CartItemResponse(
                    itemId,
                    Guid.NewGuid(),
                    "Camera",
                    "CAMERA-01",
                    "/images/products/CAMERA-01/main.png",
                    "Camera",
                    15m,
                    3,
                    45m,
                    10,
                    true)
            ],
            3,
            45m,
            DateTimeOffset.UtcNow);
        var handler = new StubHttpMessageHandler(
            async (request, cancellationToken) =>
            {
                requestedMethod = request.Method;
                requestedPath = request.RequestUri?.AbsolutePath;
                requestPayload = await request.Content!
                    .ReadFromJsonAsync<UpdateCartItemQuantityRequest>(
                        cancellationToken);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(responseCart)
                };
            });
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.test/")
        };
        var service = new CartApiService(
            client,
            NullLogger<CartApiService>.Instance);

        var result = await service.UpdateItemAsync(
            itemId,
            new UpdateCartItemQuantityRequest
            {
                Quantity = 3
            });

        Assert.True(result.IsSuccess);
        Assert.Equal(HttpMethod.Put, requestedMethod);
        Assert.Equal(
            $"/api/cart/items/{itemId:D}",
            requestedPath);
        Assert.Equal(3, requestPayload?.Quantity);
        Assert.Equal(3, result.Data?.TotalQuantity);
        Assert.Equal(45m, result.Data?.TotalAmount);
    }
}
```

# 7. Kod Açıklaması

`CartService`, ana görseli önce `IsPrimary`, sonra `SortOrder` değerine göre
seçer. Birim fiyat, satır toplamı, toplam adet ve toplam tutar yalnızca API
tarafında hesaplanır.

MVC `CartController.Update`, normal form isteğinde eski redirect davranışını
korur. `Accept: application/json` veya `X-Requested-With: XMLHttpRequest`
başlığı geldiğinde güncel `CartResponse` JSON olarak döner.

`cart.js`, input değişikliklerini kısa süre bekleterek gereksiz istekleri azaltır.
İstek sürerken kontrolü kilitler, hata oluşursa son başarılı değere döner ve
erişilebilir canlı durum mesajı üretir. Başarılı response sonrasında DOM'daki
bütün toplam alanları ve navbar rozeti tek kaynaktan güncellenir.

# 8. API–MVC Veri Akışı

```text
Sepet adet input / + / −
    ↓
cart.js + FormData + Anti-forgery token
    ↓
POST /cart/items/{itemId}/quantity
    ↓
MVC CartController
    ↓
ICartApiService
    ↓
PUT /api/cart/items/{itemId}
    ↓
API CartService
    ↓
Cart + Product + ProductImages
    ↓
SQL Server
    ↓
CartResponse JSON
    ↓
Satır toplamı + toplam adet + genel toplam + navbar rozeti
```

# 9. Uygulama Sırası

1. API ve MVC cart item response modellerini güncelle.
2. Cart API sorgusuna görselleri ekle.
3. MVC controller'ın JSON response desteğini ekle.
4. Navbar ViewComponent'ini oluştur.
5. Sepet Razor görünümünü güncelle.
6. Harici `cart.js` dosyasını ekle.
7. Responsive CSS kurallarını ekle.
8. Unit ve MVC servis testlerini çalıştır.

# 10. Çalıştırma ve Test

Customer hesabıyla giriş yap:

```text
https://localhost:7002/account/login
```

Bir ürünü sepete ekleyip şu adresi aç:

```text
https://localhost:7002/cart
```

Artı/eksi düğmesine basıldığında veya adet alanı değiştirildiğinde sayfa
yenilenmeden şu alanlar değişmelidir:

```text
Ürün satırı ara toplamı
Farklı ürün sayısı
Toplam ürün adedi
Ara toplam
Toplam
Navbar sepet rozeti
```

# 11. Beklenen Sonuç

Sepetteki her ürün ana fotoğrafı, adı, SKU'su, stok bilgisi, birim fiyatı ve satır
toplamıyla görünür. Adet değişikliği `200 OK` JSON response alır. Hesaplanan
değerler sayfa yenilenmeden görünür ve stok sınırının dışına çıkılamaz.

Doğrulanan sonuç:

```text
25/25 otomatik test başarılı
JavaScript syntax kontrolü başarılı
JSON quantity update: 200
Ürün görseli: 200
Satır toplamı: doğru
Toplam adet: doğru
Genel toplam: doğru
Navbar rozeti: mevcut ve senkron
```

# 12. Yaygın Hatalar

- Değişiklik görünmüyorsa eski `dotnet watch` süreçlerini `Ctrl+C` ile kapatıp
  API ve MVC'yi yeniden başlat.
- API çalışmıyorsa navbar rozeti ve sepet verisi yüklenemez.
- `400` alınırsa adet 1–99 veya mevcut stok sınırının dışındadır.
- `409` alınırsa stok ya da sepet başka bir istek tarafından değişmiştir.
- `400 antiforgery` alınırsa form token'ı `FormData` ile gönderilmemiştir.
- CSP nedeniyle JavaScript inline yazılmamalı; `wwwroot/js/cart.js` kullanılmalıdır.

# 13. Tamamlama Kontrol Listesi

```text
[x] Ürünler sepette fotoğraflarıyla gösteriliyor
[x] Artı ve eksi adet kontrolleri çalışıyor
[x] Manuel adet değişikliği otomatik gönderiliyor
[x] CSRF koruması korunuyor
[x] Satır ara toplamı otomatik güncelleniyor
[x] Toplam ürün adedi otomatik güncelleniyor
[x] Ara toplam ve genel toplam otomatik güncelleniyor
[x] Navbar sepet rozeti otomatik güncelleniyor
[x] Fiyat ve toplamlar yalnızca API'de hesaplanıyor
[x] MVC doğrudan SQL Server'a erişmiyor
[x] Responsive görünüm eklendi
[x] Testler başarıyla geçti
```