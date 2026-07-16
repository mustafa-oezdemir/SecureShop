using SecureShop.Mvc.Http;
using SecureShop.Mvc.Models.Responses;

namespace SecureShop.Mvc.Services.Interfaces;

public interface IAuthApiService
{
    Task<ApiResponse<AuthSessionResponse>> GetSessionAsync(
        CancellationToken cancellationToken = default);
}
