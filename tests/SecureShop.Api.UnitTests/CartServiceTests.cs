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
