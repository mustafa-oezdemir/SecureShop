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
