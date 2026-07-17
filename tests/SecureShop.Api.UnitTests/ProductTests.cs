using SecureShop.Api.Domain.Entities;

namespace SecureShop.Api.UnitTests;

public sealed class ProductTests
{
    [Fact]
    public void DecreaseStock_UsesServerSideStock()
    {
        var product = CreateProduct(stock: 10);

        product.DecreaseStock(3);

        Assert.Equal(7, product.StockQuantity);
    }

    [Fact]
    public void DecreaseStock_RejectsInsufficientStock()
    {
        var product = CreateProduct(stock: 2);

        Assert.Throws<InvalidOperationException>(
            () => product.DecreaseStock(3));
    }

    [Fact]
    public void IncreaseStock_RestoresCancelledQuantity()
    {
        var product = CreateProduct(stock: 5);

        product.IncreaseStock(4);

        Assert.Equal(9, product.StockQuantity);
    }

    [Fact]
    public void AddImage_RejectsDuplicateSortOrder()
    {
        var product = CreateProduct(stock: 5);
        product.AddImage(
            "/images/products/TEST/1.png",
            "Test image",
            0,
            isPrimary: true);

        Assert.Throws<InvalidOperationException>(
            () => product.AddImage(
                "/images/products/TEST/2.png",
                "Second image",
                0));
    }

    private static Product CreateProduct(int stock) =>
        new(
            Guid.NewGuid(),
            "Test product",
            "TEST-SKU",
            19.99m,
            stock,
            "Test description");
}
