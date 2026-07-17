namespace SecureShop.Api.Domain.Entities;

public sealed class AuditLog
{
    private AuditLog()
    {
    }

    public AuditLog(
        Guid? userId,
        string action,
        string entityType,
        string? entityId,
        string? detailsJson,
        string? ipAddress)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Action = Normalize(action, 100, nameof(action));
        EntityType = Normalize(
            entityType,
            100,
            nameof(entityType));
        EntityId = NormalizeOptional(entityId, 100);
        DetailsJson = NormalizeOptional(detailsJson, 4000);
        IpAddress = NormalizeOptional(ipAddress, 64);
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid? UserId { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public string EntityType { get; private set; } = string.Empty;

    public string? EntityId { get; private set; }

    public string? DetailsJson { get; private set; }

    public string? IpAddress { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    private static string Normalize(
        string value,
        int maximumLength,
        string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            value,
            parameterName);

        return NormalizeOptional(value, maximumLength)!;
    }

    private static string? NormalizeOptional(
        string? value,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();

        return normalized.Length <= maximumLength
            ? normalized
            : normalized[..maximumLength];
    }
}
