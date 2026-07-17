namespace SecureShop.Api.Features.Audit;

public interface IAuditService
{
    void Record(
        string action,
        string entityType,
        string? entityId,
        object? details = null);
}
