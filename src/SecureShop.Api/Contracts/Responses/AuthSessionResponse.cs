namespace SecureShop.Api.Contracts.Responses;

public sealed record AuthSessionResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    string[] Roles,
    bool TwoFactorEnabled);
