using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SecureShop.Mvc.Http;
using SecureShop.Mvc.Models.Requests;
using SecureShop.Mvc.Models.Responses;
using SecureShop.Mvc.Services.Interfaces;

namespace SecureShop.Mvc.Services.Api;

public sealed class CartApiService : ICartApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CartApiService> _logger;

    public CartApiService(
        HttpClient httpClient,
        ILogger<CartApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<ApiResponse<CartResponse>> GetAsync(
        CancellationToken cancellationToken = default) =>
        SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "api/cart"),
            cancellationToken);

    public Task<ApiResponse<CartResponse>> AddItemAsync(
        AddCartItemRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            new HttpRequestMessage(HttpMethod.Post, "api/cart/items")
            {
                Content = JsonContent.Create(request)
            },
            cancellationToken);

    public Task<ApiResponse<CartResponse>> UpdateItemAsync(
        Guid itemId,
        UpdateCartItemQuantityRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            new HttpRequestMessage(
                HttpMethod.Put,
                $"api/cart/items/{itemId:D}")
            {
                Content = JsonContent.Create(request)
            },
            cancellationToken);

    public Task<ApiResponse<CartResponse>> RemoveItemAsync(
        Guid itemId,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            new HttpRequestMessage(
                HttpMethod.Delete,
                $"api/cart/items/{itemId:D}"),
            cancellationToken);

    public Task<ApiResponse<CartResponse>> ClearAsync(
        CancellationToken cancellationToken = default) =>
        SendAsync(
            new HttpRequestMessage(HttpMethod.Delete, "api/cart"),
            cancellationToken);

    private async Task<ApiResponse<CartResponse>> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using (request)
        {
            try
            {
                using var response = await _httpClient.SendAsync(
                    request,
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return ApiResponse<CartResponse>.Failure(
                        response.StatusCode,
                        await GetErrorMessageAsync(
                            response,
                            cancellationToken));
                }

                var cart = await response.Content
                    .ReadFromJsonAsync<CartResponse>(
                        cancellationToken: cancellationToken);

                return cart is null
                    ? ApiResponse<CartResponse>.Failure(
                        HttpStatusCode.BadGateway,
                        "API geçerli bir sepet response'u döndürmedi.")
                    : ApiResponse<CartResponse>.Success(
                        response.StatusCode,
                        cart);
            }
            catch (HttpRequestException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Sepet API isteği tamamlanamadı.");

                return ApiResponse<CartResponse>.Failure(
                    HttpStatusCode.ServiceUnavailable,
                    "SecureShop API hizmetine ulaşılamıyor.");
            }
            catch (JsonException exception)
            {
                _logger.LogError(
                    exception,
                    "Sepet API response'u okunamadı.");

                return ApiResponse<CartResponse>.Failure(
                    HttpStatusCode.BadGateway,
                    "API sepet response formatı geçersiz.");
            }
            catch (OperationCanceledException)
                when (!cancellationToken.IsCancellationRequested)
            {
                return ApiResponse<CartResponse>.Failure(
                    HttpStatusCode.GatewayTimeout,
                    "Sepet API isteği zaman aşımına uğradı.");
            }
        }
    }

    private static async Task<string> GetErrorMessageAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var problem = await response.Content
                .ReadFromJsonAsync<ProblemDetails>(
                    cancellationToken: cancellationToken);

            if (!string.IsNullOrWhiteSpace(problem?.Detail))
            {
                return problem.Detail;
            }
        }
        catch (JsonException)
        {
            return GetFallbackErrorMessage(response.StatusCode);
        }

        return GetFallbackErrorMessage(response.StatusCode);
    }

    private static string GetFallbackErrorMessage(
        HttpStatusCode statusCode) =>
        statusCode switch
        {
            HttpStatusCode.Unauthorized =>
                "Sepeti görüntülemek için giriş yapmalısınız.",
            HttpStatusCode.Forbidden =>
                "Sepet işlemleri yalnızca müşteri hesaplarına açıktır.",
            HttpStatusCode.NotFound =>
                "Sepet öğesi bulunamadı.",
            HttpStatusCode.Conflict =>
                "Sepet güncellenemedi. Stok durumunu kontrol edin.",
            _ => "Sepet işlemi tamamlanamadı."
        };
}
