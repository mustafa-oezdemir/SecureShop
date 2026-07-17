using Microsoft.EntityFrameworkCore;
using SecureShop.Api.Contracts.Responses;
using SecureShop.Api.Data;

namespace SecureShop.Api.Features.Audit;

public sealed class AuditQueryService : IAuditQueryService
{
    private readonly AppDbContext _dbContext;

    public AuditQueryService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<AuditLogResponse>>
        GetLatestAsync(
            int take,
            CancellationToken cancellationToken)
    {
        var safeTake = Math.Clamp(take, 1, 500);

        return await _dbContext.AuditLogs
            .AsNoTracking()
            .OrderByDescending(log => log.CreatedAtUtc)
            .Take(safeTake)
            .Select(log => new AuditLogResponse(
                log.Id,
                log.UserId,
                log.Action,
                log.EntityType,
                log.EntityId,
                log.DetailsJson,
                log.IpAddress,
                log.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }
}
