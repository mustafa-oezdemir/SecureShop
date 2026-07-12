namespace SecureShop.Api.Features.Auth.External;

public enum GoogleExternalLoginStatus
{
    Succeeded,
    RequiresTwoFactor,
    LockedOut,
    NotAllowed,
    Inactive,
    EmailNotAvailable,
    EmailNotVerified,
    EmailAlreadyRegistered,
    Failed
}

public sealed record GoogleExternalLoginResult(
    GoogleExternalLoginStatus Status,
    string? Email = null,
    string? DisplayName = null,
    IReadOnlyCollection<string>? Roles = null);
