using Google.Apis.Auth.AspNetCore3;
using Microsoft.AspNetCore.Identity;

namespace SecureShop.Api.Features.Auth.External;

public static class GoogleAuthenticationServiceCollectionExtensions
{
    private const string ClientIdConfigurationKey =
        "Authentication:Google:ClientId";

    private const string ClientSecretConfigurationKey =
        "Authentication:Google:ClientSecret";

    private const string CallbackPath =
        "/signin-google";

    public static IServiceCollection
        AddSecureShopGoogleAuthentication(
            this IServiceCollection services,
            IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var clientId =
            configuration[ClientIdConfigurationKey];

        var clientSecret =
            configuration[ClientSecretConfigurationKey];

        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new InvalidOperationException(
                $"'{ClientIdConfigurationKey}' yapilandirmasi bulunamadi.");
        }

        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException(
                $"'{ClientSecretConfigurationKey}' yapilandirmasi bulunamadi.");
        }

        services
            .AddAuthentication()
            .AddGoogleOpenIdConnect(options =>
            {
                options.ClientId = clientId;
                options.ClientSecret = clientSecret;

                options.CallbackPath = CallbackPath;

                options.SignInScheme =
                    IdentityConstants.ExternalScheme;

                options.SaveTokens = false;

                options.GetClaimsFromUserInfoEndpoint = true;

                if (!options.Scope.Contains("email"))
                {
                    options.Scope.Add("email");
                }

                if (!options.Scope.Contains("profile"))
                {
                    options.Scope.Add("profile");
                }
            });

        services.AddScoped<
            IGoogleExternalLoginService,
            GoogleExternalLoginService>();

        return services;
    }
}
