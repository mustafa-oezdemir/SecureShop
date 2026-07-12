namespace SecureShop.Api.Contracts.Responses;

public sealed record GoogleExternalLoginResponse(
    string Status,
    string Email,
    string DisplayName,
    IReadOnlyCollection<string> Roles);
