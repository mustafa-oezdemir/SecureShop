namespace SecureShop.Api.Contracts.Responses;

public sealed record AuditLogResponse(
    Guid Id,
    Guid? UserId,
    string Action,
    string EntityType,
    string? EntityId,
    string? DetailsJson,
    string? IpAddress,
    DateTimeOffset CreatedAtUtc);
