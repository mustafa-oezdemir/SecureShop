using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using SecureShop.Api.Data;
using SecureShop.Api.Security.Identity.Tokens;

namespace SecureShop.Api.Security.Identity;

public static class IdentityServiceCollectionExtensions
{
    private const int PasswordRequiredLength = 12;
    private const int PasswordRequiredUniqueCharacters = 4;
    private const int PasswordHasherIterationCount = 150_000;
    private const int MaximumFailedAccessAttempts = 5;

    private static readonly TimeSpan LockoutDuration =
        TimeSpan.FromMinutes(15);

    private static readonly TimeSpan AuthenticationCookieLifetime =
        TimeSpan.FromMinutes(20);

    private static readonly TimeSpan SecurityStampValidationInterval =
        TimeSpan.FromMinutes(5);

    private static readonly TimeSpan PasswordResetTokenLifetime =
        TimeSpan.FromHours(1);

    public static IServiceCollection AddSecureShopIdentity(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddIdentity<ApplicationUser, ApplicationRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders()
            .AddTokenProvider<
                EmailConfirmationTokenProvider<ApplicationUser>>(
                AppTokenProviders.EmailConfirmation);

        services.AddOptions<
            EmailConfirmationTokenProviderOptions>();

        services.Configure<IdentityOptions>(options =>
        {
            ConfigureClaims(options);
            ConfigurePassword(options);
            ConfigureLockout(options);
            ConfigureSignIn(options);
            ConfigureUser(options);
            ConfigureTokens(options);
        });

        services.Configure<PasswordHasherOptions>(options =>
        {
            options.CompatibilityMode =
                PasswordHasherCompatibilityMode.IdentityV3;

            options.IterationCount =
                PasswordHasherIterationCount;
        });

        services.Configure<SecurityStampValidatorOptions>(options =>
        {
            options.ValidationInterval =
                SecurityStampValidationInterval;
        });

        services.Configure<DataProtectionTokenProviderOptions>(
            options =>
            {
                options.TokenLifespan =
                    PasswordResetTokenLifetime;
            });

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name =
                "__Host-SecureShop.Api.Auth";

            options.Cookie.HttpOnly = true;

            options.Cookie.SecurePolicy =
                CookieSecurePolicy.Always;

            options.Cookie.SameSite =
                SameSiteMode.Lax;

            options.Cookie.Path = "/";

            options.Cookie.IsEssential = true;

            options.ExpireTimeSpan =
                AuthenticationCookieLifetime;

            options.SlidingExpiration = true;

            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode =
                    StatusCodes.Status401Unauthorized;

                return Task.CompletedTask;
            };

            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode =
                    StatusCodes.Status403Forbidden;

                return Task.CompletedTask;
            };
        });

        return services;
    }

    private static void ConfigureClaims(
        IdentityOptions options)
    {
        options.ClaimsIdentity.UserIdClaimType =
            ClaimTypes.NameIdentifier;

        options.ClaimsIdentity.UserNameClaimType =
            ClaimTypes.Name;

        options.ClaimsIdentity.RoleClaimType =
            ClaimTypes.Role;
    }

    private static void ConfigurePassword(
        IdentityOptions options)
    {
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireUppercase = true;
        options.Password.RequiredLength =
            PasswordRequiredLength;
        options.Password.RequiredUniqueChars =
            PasswordRequiredUniqueCharacters;
    }

    private static void ConfigureLockout(
        IdentityOptions options)
    {
        options.Lockout.AllowedForNewUsers = true;

        options.Lockout.DefaultLockoutTimeSpan =
            LockoutDuration;

        options.Lockout.MaxFailedAccessAttempts =
            MaximumFailedAccessAttempts;
    }

    private static void ConfigureSignIn(
        IdentityOptions options)
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.SignIn.RequireConfirmedEmail = true;
        options.SignIn.RequireConfirmedPhoneNumber = false;
    }

    private static void ConfigureUser(
        IdentityOptions options)
    {
        options.User.AllowedUserNameCharacters =
            "abcdefghijklmnopqrstuvwxyz" +
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ" +
            "0123456789-._@+";

        options.User.RequireUniqueEmail = true;
    }

    private static void ConfigureTokens(
        IdentityOptions options)
    {
        options.Tokens.EmailConfirmationTokenProvider =
            AppTokenProviders.EmailConfirmation;

        options.Tokens.PasswordResetTokenProvider =
            TokenOptions.DefaultProvider;
    }
}
