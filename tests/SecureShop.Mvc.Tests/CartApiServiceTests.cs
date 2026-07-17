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
