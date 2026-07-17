using System.Text.Json;
using SecureShop.Api.Data;
using SecureShop.Api.Domain.Entities;
using SecureShop.Api.Security;

namespace SecureShop.Api.Features.Audit;

public sealed class AuditService : IAuditService
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditService(
        AppDbContext dbContext,
        ICurrentUserService currentUser,
        IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _httpContextAccessor = httpContextAccessor;
    }

    public void Record(
        string action,
        string entityType,
        string? entityId,
        object? details = null)
    {
        var ipAddress = _httpContextAccessor
            .HttpContext?
            .Connection
            .RemoteIpAddress?
            .ToString();

        var detailsJson = details is null
            ? null
            : JsonSerializer.Serialize(details, SerializerOptions);

        _dbContext.AuditLogs.Add(new AuditLog(
            _currentUser.UserId,
            action,
            entityType,
            entityId,
            detailsJson,
            ipAddress));
    }
}
