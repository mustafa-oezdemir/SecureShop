using SecureShop.Mvc.Http;
using SecureShop.Mvc.Models.Responses;

namespace SecureShop.Mvc.Services.Interfaces;

public interface IAuditApiService
{
    Task<ApiResponse<IReadOnlyList<AuditLogResponse>>> GetLatestAsync(
        int take = 200,
        CancellationToken cancellationToken = default);
}
