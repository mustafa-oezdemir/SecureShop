using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SecureShop.Mvc.Http;
using SecureShop.Mvc.Models.Responses;
using SecureShop.Mvc.Services.Interfaces;

namespace SecureShop.Mvc.Services.Api;

public sealed class AuthApiService : IAuthApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AuthApiService> _logger;

    public AuthApiService(
        HttpClient httpClient,
        ILogger<AuthApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<LoginApiResult> LoginAsync(string email,string password,CancellationToken cancellationToken=default)
    {
        try
        {
            using var response=await _httpClient.PostAsJsonAsync("api/auth/local/login",new { email,password },cancellationToken);
            string? cookie=null;
            if(response.Headers.TryGetValues("Set-Cookie",out var values))
                cookie=values.FirstOrDefault(value=>value.StartsWith("__Host-SecureShop.Auth=",StringComparison.Ordinal));
            if(response.IsSuccessStatusCode && cookie is not null) return new(true,response.StatusCode,cookie,null);
            var message=response.StatusCode switch { (HttpStatusCode)423=>"Hesap geçici olarak kilitlendi.",HttpStatusCode.Conflict=>"İki faktörlü doğrulama gerekli.",HttpStatusCode.TooManyRequests=>"Çok fazla deneme yapıldı.",_=>"E-posta veya parola geçersiz."};
            return new(false,response.StatusCode,null,message);
        }
        catch(HttpRequestException exception){_logger.LogWarning(exception,"Yerel login API isteği tamamlanamadı.");return new(false,HttpStatusCode.ServiceUnavailable,null,"API hizmetine ulaşılamıyor.");}
    }

    public async Task<ApiResponse<AuthSessionResponse>> GetSessionAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(
                "api/auth/session",
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return ApiResponse<AuthSessionResponse>.Failure(
                    response.StatusCode,
                    "API oturumu bulunamadı.");
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return ApiResponse<AuthSessionResponse>.Failure(
                    response.StatusCode,
                    "API bu hesap için erişimi reddetti.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return ApiResponse<AuthSessionResponse>.Failure(
                    response.StatusCode,
                    "API oturum bilgisi alınamadı.");
            }

            var session =
                await response.Content.ReadFromJsonAsync<AuthSessionResponse>(
                    cancellationToken: cancellationToken);

            if (session is null)
            {
                return ApiResponse<AuthSessionResponse>.Failure(
                    HttpStatusCode.BadGateway,
                    "API geçerli bir oturum response'u döndürmedi.");
            }

            return ApiResponse<AuthSessionResponse>.Success(
                response.StatusCode,
                session);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                exception,
                "SecureShop API oturum isteği tamamlanamadı.");

            return ApiResponse<AuthSessionResponse>.Failure(
                HttpStatusCode.ServiceUnavailable,
                "SecureShop API hizmetine ulaşılamıyor.");
        }
        catch (JsonException exception)
        {
            _logger.LogError(
                exception,
                "SecureShop API oturum response'u okunamadı.");

            return ApiResponse<AuthSessionResponse>.Failure(
                HttpStatusCode.BadGateway,
                "API oturum response formatı geçersiz.");
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return ApiResponse<AuthSessionResponse>.Failure(
                HttpStatusCode.GatewayTimeout,
                "SecureShop API isteği zaman aşımına uğradı.");
        }
    }
}
