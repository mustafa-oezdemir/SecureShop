using SecureShop.Api.Features.QrCodes;
using SecureShop.Api.Security.Identity;

namespace SecureShop.Api.Features.Auth.TwoFactor;

public static class TwoFactorServiceCollectionExtensions
{
    public static IServiceCollection AddSecureShopTwoFactor(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<TotpOptions>()
            .Bind(
                configuration.GetSection(
                    TotpOptions.SectionName))
            .Validate(
                options =>
                    !string.IsNullOrWhiteSpace(options.Issuer),
                "TOTP issuer degeri bos olamaz.")
            .Validate(
                options =>
                    options.Issuer is not null
                    && options.Issuer.Trim().Length <= 64,
                "TOTP issuer degeri 64 karakterden uzun olamaz.")
            .ValidateOnStart();

        services.AddSingleton<
            IQrCodeGenerator,
            PngQrCodeGenerator>();

        services.AddScoped<
            IAuthenticatorEnrollmentService,
            AuthenticatorEnrollmentService>();

        return services;
    }
}
