using Microsoft.AspNetCore.Identity;

namespace SecureShop.Api.Features.Auth.External;

public interface IGoogleExternalLoginService
{
    Task<GoogleExternalLoginResult> CompleteAsync(
        ExternalLoginInfo externalLoginInfo,
        CancellationToken cancellationToken = default);
}
