using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SecureShop.Mvc.Models.Requests;
using SecureShop.Mvc.Models.Responses;
using SecureShop.Mvc.Services.Api;

namespace SecureShop.Mvc.Tests;

public sealed class OrderApiServiceTests
{
    [Fact]
    public async Task Create_DoesNotSendUserPriceOrTotal()
    {
        string? requestJson = null;
        var responseModel = new OrderResponse(
            Guid.NewGuid(),
            "SSH-20260717-ABC12345",
            Guid.NewGuid(),
            "Customer",
            "Street 1",
            "10115",
            "Berlin",
            "Germany",
            "PendingApproval",
            29.90m,
            [],
            DateTimeOffset.UtcNow,
            null,
            null,
            Convert.ToBase64String(new byte[8]),
            null);
        var handler = new StubHttpMessageHandler(
            async (request, cancellationToken) =>
            {
                requestJson = await request.Content!
                    .ReadAsStringAsync(cancellationToken);

                return new HttpResponseMessage(
                    HttpStatusCode.Created)
                {
                    Content = JsonContent.Create(responseModel)
                };
            });
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.test/")
        };
        var service = new OrderApiService(
            client,
            NullLogger<OrderApiService>.Instance);

        var result = await service.CreateAsync(
            new CreateOrderRequest(
                "Customer",
                "Street 1",
                "10115",
                "Berlin",
                "Germany"));

        Assert.True(result.IsSuccess);
        using var json = JsonDocument.Parse(requestJson!);
        Assert.False(json.RootElement.TryGetProperty(
            "userId",
            out _));
        Assert.False(json.RootElement.TryGetProperty(
            "totalAmount",
            out _));
        Assert.False(json.RootElement.TryGetProperty(
            "unitPrice",
            out _));
    }
}
