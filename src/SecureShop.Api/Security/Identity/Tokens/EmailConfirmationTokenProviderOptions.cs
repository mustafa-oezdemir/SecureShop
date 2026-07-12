using Microsoft.AspNetCore.Identity;

namespace SecureShop.Api.Security.Identity.Tokens;

public sealed class EmailConfirmationTokenProviderOptions
    : DataProtectionTokenProviderOptions
{
    public EmailConfirmationTokenProviderOptions()
    {
        Name = AppTokenProviders.EmailConfirmation;
        TokenLifespan = TimeSpan.FromHours(4);
    }
}
