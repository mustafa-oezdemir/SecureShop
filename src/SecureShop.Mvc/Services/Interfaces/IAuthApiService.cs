using SecureShop.Mvc.Http;
using SecureShop.Mvc.Models.Responses;

namespace SecureShop.Mvc.Services.Interfaces;

public interface IAuthApiService
{
    Task<LoginApiResult> LoginAsync(string email,string password,CancellationToken cancellationToken=default);
    Task<ApiResponse<AuthSessionResponse>> GetSessionAsync(
        CancellationToken cancellationToken = default);
}
