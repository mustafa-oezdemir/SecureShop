using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SecureShop.Mvc.Models.Responses;
using SecureShop.Mvc.Services.Api;

namespace SecureShop.Mvc.Tests;

public sealed class ProductApiServiceTests
{
    [Fact]
    public async Task GetProductBySku_UsesEncodedSkuRoute()
    {
        Uri? requestedUri = null;
        var expected = new ProductResponse(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Elektronik",
            "Product",
            "SKU-1",
            null,
            10m,
            2,
            true,
            [],
            DateTimeOffset.UtcNow,
            null,
            Convert.ToBase64String(new byte[8]));
        var handler = new StubHttpMessageHandler(
            (request, _) =>
            {
                requestedUri = request.RequestUri;
                return Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(expected)
                    });
            });
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.test/")
        };
        var service = new ProductApiService(
            client,
            NullLogger<ProductApiService>.Instance);

        var result = await service.GetProductBySkuAsync("SKU-1");

        Assert.True(result.IsSuccess);
        Assert.Equal(
            "/api/products/by-sku/SKU-1",
            requestedUri?.AbsolutePath);
    }

    [Fact]
    public async Task GetProductBySku_MapsNotFound()
    {
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(
                new HttpResponseMessage(
                    HttpStatusCode.NotFound)));
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.test/")
        };
        var service = new ProductApiService(
            client,
            NullLogger<ProductApiService>.Instance);

        var result = await service.GetProductBySkuAsync(
            "MISSING");

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
    }
}
