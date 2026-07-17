using System.Net;
using System.Net.Http.Json;
using SecureShop.Api.Contracts.Responses;

namespace SecureShop.Api.IntegrationTests;

public sealed class ProductEndpointTests
    : IClassFixture<SecureShopApiFactory>
{
    private readonly SecureShopApiFactory _factory;

    public ProductEndpointTests(SecureShopApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetProducts_ReturnsPublicCatalog()
    {
        using var client = _factory.CreateHttpsClient();

        using var response = await client.GetAsync("/api/products");
        var products = await response.Content
            .ReadFromJsonAsync<List<ProductResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(products);
        Assert.Contains(
            products,
            product => product.Sku == "INTEGRATION-SKU");
    }

    [Fact]
    public async Task GetBySku_ReturnsExpectedProduct()
    {
        using var client = _factory.CreateHttpsClient();

        using var response = await client.GetAsync(
            "/api/products/by-sku/INTEGRATION-SKU");
        var product = await response.Content
            .ReadFromJsonAsync<ProductResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("INTEGRATION-SKU", product?.Sku);
    }

    [Fact]
    public async Task UnknownSku_ReturnsNotFound()
    {
        using var client = _factory.CreateHttpsClient();

        using var response = await client.GetAsync(
            "/api/products/by-sku/DOES-NOT-EXIST");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }
}
