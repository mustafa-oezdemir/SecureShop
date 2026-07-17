using System.Net;

namespace SecureShop.Api.IntegrationTests;

public sealed class SecurityEndpointTests
    : IClassFixture<SecureShopApiFactory>
{
    private readonly SecureShopApiFactory _factory;

    public SecurityEndpointTests(SecureShopApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Cart_RequiresAuthentication()
    {
        using var client = _factory.CreateHttpsClient();

        using var response = await client.GetAsync("/api/cart");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task ApiResponse_ContainsSecurityHeaders()
    {
        using var client = _factory.CreateHttpsClient();

        using var response = await client.GetAsync("/api/products");

        Assert.True(response.Headers.Contains(
            "X-Content-Type-Options"));
        Assert.True(response.Headers.Contains(
            "Content-Security-Policy"));
        Assert.True(response.Headers.Contains(
            "Referrer-Policy"));
    }
}
