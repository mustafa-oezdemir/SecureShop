using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SecureShop.Api.Contracts.Requests;
using SecureShop.Api.Data;
using SecureShop.Api.Domain.Entities;
using SecureShop.Api.Features.Audit;
using SecureShop.Api.Features.Orders;
using SecureShop.Api.Features.QrCodes;

namespace SecureShop.Api.UnitTests;

public sealed class OrderServiceTests
{
    [Fact]
    public async Task CreateAsync_UsesServerPriceAndClearsCart()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var category = new Category("Test");
        var product = new Product(
            category.Id,
            "Server priced product",
            "SERVER-PRICE-01",
            49.90m,
            10,
            "Test product");
        var cart = new Cart(userId);

        cart.AddItem(product.Id, 2);
        dbContext.AddRange(category, product, cart);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.CreateAsync(
            userId,
            ValidRequest(),
            CancellationToken.None);

        Assert.Equal(
            OrderMutationStatus.Succeeded,
            result.Status);
        Assert.NotNull(result.Order);
        Assert.Equal(99.80m, result.Order.TotalAmount);
        Assert.Equal(8, product.StockQuantity);
        Assert.Empty(await dbContext.CartItems.ToListAsync());
        Assert.Single(await dbContext.Orders.ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_RejectsInsufficientStockWithoutMutation()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var category = new Category("Test");
        var product = new Product(
            category.Id,
            "Limited product",
            "LIMITED-01",
            10m,
            1,
            "Test product");
        var cart = new Cart(userId);

        cart.AddItem(product.Id, 2);
        dbContext.AddRange(category, product, cart);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var result = await service.CreateAsync(
            userId,
            ValidRequest(),
            CancellationToken.None);

        Assert.Equal(
            OrderMutationStatus.InsufficientStock,
            result.Status);
        Assert.Equal(1, product.StockQuantity);
        Assert.Single(await dbContext.CartItems.ToListAsync());
        Assert.Empty(await dbContext.Orders.ToListAsync());
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(
                $"SecureShop-OrderService-{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static OrderService CreateService(
        AppDbContext dbContext) =>
        new(
            dbContext,
            new NullAuditService(),
            new StubQrTokenService(),
            new StubQrCodeGenerator(),
            Options.Create(new OrderQrOptions()));

    private static CreateOrderRequest ValidRequest() =>
        new()
        {
            RecipientName = "Test Customer",
            AddressLine = "Teststrasse 10",
            PostalCode = "10115",
            City = "Berlin",
            Country = "Deutschland"
        };

    private sealed class NullAuditService : IAuditService
    {
        public void Record(
            string action,
            string entityType,
            string? entityId,
            object? details = null)
        {
        }
    }

    private sealed class StubQrTokenService
        : IOrderQrTokenService
    {
        public string Generate(Guid orderId) =>
            orderId.ToString("N");

        public bool TryValidate(
            string token,
            out Guid orderId) =>
            Guid.TryParseExact(token, "N", out orderId);
    }

    private sealed class StubQrCodeGenerator : IQrCodeGenerator
    {
        public string GeneratePngDataUrl(string content) =>
            $"data:image/png;base64,{content}";
    }
}
