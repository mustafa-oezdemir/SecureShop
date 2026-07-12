using SecureShop.Api.Contracts.Responses;
using SecureShop.Api.Security.Identity;

namespace SecureShop.Api.Features.Auth.TwoFactor;

public interface IAuthenticatorEnrollmentService
{
    Task<AuthenticatorSetupResponse> CreateSetupAsync(
        ApplicationUser user,
        CancellationToken cancellationToken = default);

    Task<EnableAuthenticatorResponse?> EnableAsync(
        ApplicationUser user,
        string verificationCode,
        CancellationToken cancellationToken = default);
}
