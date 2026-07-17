using SecureShop.Api.Contracts.Responses;

namespace SecureShop.Api.Features.Audit;

public interface IAuditQueryService
{
    Task<IReadOnlyList<AuditLogResponse>> GetLatestAsync(
        int take,
        CancellationToken cancellationToken);
}
