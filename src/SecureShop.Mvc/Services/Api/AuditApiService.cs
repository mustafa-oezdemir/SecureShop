using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SecureShop.Mvc.Http;
using SecureShop.Mvc.Models.Responses;
using SecureShop.Mvc.Services.Interfaces;

namespace SecureShop.Mvc.Services.Api;

public sealed class AuditApiService : IAuditApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AuditApiService> _logger;

    public AuditApiService(
        HttpClient httpClient,
        ILogger<AuditApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ApiResponse<IReadOnlyList<AuditLogResponse>>>
        GetLatestAsync(
            int take = 200,
            CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(
                $"api/audit-logs?take={Math.Clamp(take, 1, 500)}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ApiResponse<IReadOnlyList<AuditLogResponse>>
                    .Failure(
                        response.StatusCode,
                        "Audit kayıtları alınamadı.");
            }

            var logs = await response.Content
                .ReadFromJsonAsync<List<AuditLogResponse>>(
                    cancellationToken: cancellationToken);

            return logs is null
                ? ApiResponse<IReadOnlyList<AuditLogResponse>>
                    .Failure(
                        HttpStatusCode.BadGateway,
                        "API geçerli audit response'u döndürmedi.")
                : ApiResponse<IReadOnlyList<AuditLogResponse>>
                    .Success(response.StatusCode, logs);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                exception,
                "Audit API isteği tamamlanamadı.");

            return ApiResponse<IReadOnlyList<AuditLogResponse>>
                .Failure(
                    HttpStatusCode.ServiceUnavailable,
                    "SecureShop API hizmetine ulaşılamıyor.");
        }
        catch (JsonException exception)
        {
            _logger.LogError(
                exception,
                "Audit API response'u okunamadı.");

            return ApiResponse<IReadOnlyList<AuditLogResponse>>
                .Failure(
                    HttpStatusCode.BadGateway,
                    "Audit response formatı geçersiz.");
        }
    }
}
