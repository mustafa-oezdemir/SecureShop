using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SecureShop.Mvc.Http;
using SecureShop.Mvc.Models.Requests;
using SecureShop.Mvc.Models.Responses;
using SecureShop.Mvc.Services.Interfaces;

namespace SecureShop.Mvc.Services.Api;

public sealed class OrderApiService : IOrderApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OrderApiService> _logger;

    public OrderApiService(
        HttpClient httpClient,
        ILogger<OrderApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<ApiResponse<OrderResponse>> CreateAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<OrderResponse>(
            new HttpRequestMessage(HttpMethod.Post, "api/orders")
            {
                Content = JsonContent.Create(request)
            },
            cancellationToken);

    public Task<ApiResponse<IReadOnlyList<OrderResponse>>> GetMineAsync(
        CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<OrderResponse>>(
            new HttpRequestMessage(HttpMethod.Get, "api/orders"),
            cancellationToken);

    public Task<ApiResponse<OrderResponse>> GetMineAsync(
        string orderNumber,
        CancellationToken cancellationToken = default) =>
        SendAsync<OrderResponse>(
            new HttpRequestMessage(
                HttpMethod.Get,
                $"api/orders/{Encode(orderNumber)}"),
            cancellationToken);

    public Task<ApiResponse<IReadOnlyList<OrderResponse>>> GetStaffAsync(
        CancellationToken cancellationToken = default) =>
        SendAsync<IReadOnlyList<OrderResponse>>(
            new HttpRequestMessage(
                HttpMethod.Get,
                "api/employee/orders"),
            cancellationToken);

    public Task<ApiResponse<OrderResponse>> GetStaffAsync(
        string orderNumber,
        CancellationToken cancellationToken = default) =>
        SendAsync<OrderResponse>(
            new HttpRequestMessage(
                HttpMethod.Get,
                $"api/employee/orders/{Encode(orderNumber)}"),
            cancellationToken);

    public Task<ApiResponse<OrderResponse>> ApproveAsync(
        string orderNumber,
        ProcessOrderRequest request,
        CancellationToken cancellationToken = default) =>
        ProcessAsync(
            orderNumber,
            "approve",
            request,
            cancellationToken);

    public Task<ApiResponse<OrderResponse>> MarkReadyAsync(
        string orderNumber,
        ProcessOrderRequest request,
        CancellationToken cancellationToken = default) =>
        ProcessAsync(
            orderNumber,
            "ready",
            request,
            cancellationToken);

    public Task<ApiResponse<OrderResponse>> CancelAsync(
        string orderNumber,
        ProcessOrderRequest request,
        CancellationToken cancellationToken = default) =>
        ProcessAsync(
            orderNumber,
            "cancel",
            request,
            cancellationToken);

    public Task<ApiResponse<OrderResponse>> VerifyQrAsync(
        VerifyOrderQrRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<OrderResponse>(
            new HttpRequestMessage(
                HttpMethod.Post,
                "api/employee/orders/verify-qr")
            {
                Content = JsonContent.Create(request)
            },
            cancellationToken);

    private Task<ApiResponse<OrderResponse>> ProcessAsync(
        string orderNumber,
        string operation,
        ProcessOrderRequest request,
        CancellationToken cancellationToken) =>
        SendAsync<OrderResponse>(
            new HttpRequestMessage(
                HttpMethod.Post,
                $"api/employee/orders/{Encode(orderNumber)}/{operation}")
            {
                Content = JsonContent.Create(request)
            },
            cancellationToken);

    private async Task<ApiResponse<T>> SendAsync<T>(
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
                    return ApiResponse<T>.Failure(
                        response.StatusCode,
                        await ReadErrorAsync(
                            response,
                            cancellationToken));
                }

                var data = await response.Content
                    .ReadFromJsonAsync<T>(
                        cancellationToken: cancellationToken);

                return data is null
                    ? ApiResponse<T>.Failure(
                        HttpStatusCode.BadGateway,
                        "API geçerli bir sipariş response'u döndürmedi.")
                    : ApiResponse<T>.Success(
                        response.StatusCode,
                        data);
            }
            catch (HttpRequestException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Sipariş API isteği tamamlanamadı.");

                return ApiResponse<T>.Failure(
                    HttpStatusCode.ServiceUnavailable,
                    "SecureShop API hizmetine ulaşılamıyor.");
            }
            catch (JsonException exception)
            {
                _logger.LogError(
                    exception,
                    "Sipariş API response'u okunamadı.");

                return ApiResponse<T>.Failure(
                    HttpStatusCode.BadGateway,
                    "API sipariş response formatı geçersiz.");
            }
        }
    }

    private static async Task<string> ReadErrorAsync(
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
            return "Sipariş işlemi tamamlanamadı.";
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized =>
                "Sipariş işlemi için giriş yapmalısınız.",
            HttpStatusCode.Forbidden =>
                "Bu sipariş işlemi için yetkiniz yok.",
            HttpStatusCode.NotFound =>
                "Sipariş bulunamadı.",
            HttpStatusCode.Conflict =>
                "Sipariş durumu değişti. Sayfayı yenileyin.",
            _ => "Sipariş işlemi tamamlanamadı."
        };
    }

    private static string Encode(string value) =>
        Uri.EscapeDataString(value.Trim());
}
