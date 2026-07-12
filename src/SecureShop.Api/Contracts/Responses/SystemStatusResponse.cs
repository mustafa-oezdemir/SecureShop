namespace SecureShop.Api.Contracts.Responses;

public sealed record SystemStatusResponse(
    string Application,
    string Status,
    string Database,
    DateTimeOffset UtcTimestamp);
