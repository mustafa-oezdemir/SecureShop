using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureShop.Api.Contracts.Responses;
using SecureShop.Api.Features.Audit;
using SecureShop.Api.Security.Policies;

namespace SecureShop.Api.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize(Policy = AppPolicies.AdminOnly)]
public sealed class AuditLogsController : ControllerBase
{
    private readonly IAuditQueryService _auditQuery;

    public AuditLogsController(IAuditQueryService auditQuery)
    {
        _auditQuery = auditQuery;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AuditLogResponse>>> Get(
        [FromQuery] int take = 200,
        CancellationToken cancellationToken = default) =>
        Ok(await _auditQuery.GetLatestAsync(
            take,
            cancellationToken));
}
