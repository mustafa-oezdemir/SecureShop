namespace SecureShop.Api.Contracts.Responses;

public sealed record EnableAuthenticatorResponse(
    bool IsEnabled,
    IReadOnlyCollection<string> RecoveryCodes);
