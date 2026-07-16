using Microsoft.AspNetCore.DataProtection;

namespace SecureShop.Api.Security.Identity;

public static class SharedCookieDataProtectionExtensions
{
    public static IServiceCollection AddSecureShopSharedCookieDataProtection(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var keyRingPath = ResolveKeyRingPath(
            configuration,
            environment);

        Directory.CreateDirectory(keyRingPath);

        var dataProtectionBuilder = services
            .AddDataProtection()
            .PersistKeysToFileSystem(
                new DirectoryInfo(keyRingPath))
            .SetApplicationName(
                SharedCookieAuthenticationDefaults
                    .DataProtectionApplicationName);

        if (OperatingSystem.IsWindows())
        {
            dataProtectionBuilder.ProtectKeysWithDpapi();
        }
        else if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Production ortamında ortak Data Protection anahtarları için " +
                "şifreli ve kalıcı bir key store yapılandırılmalıdır.");
        }

        return services;
    }

    private static string ResolveKeyRingPath(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var configuredPath = configuration[
            SharedCookieAuthenticationDefaults
                .KeyRingPathConfigurationKey];

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var expandedPath =
                Environment.ExpandEnvironmentVariables(
                    configuredPath.Trim());

            return Path.IsPathRooted(expandedPath)
                ? Path.GetFullPath(expandedPath)
                : Path.GetFullPath(
                    expandedPath,
                    environment.ContentRootPath);
        }

        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                $"'{SharedCookieAuthenticationDefaults.KeyRingPathConfigurationKey}' " +
                "production ortamında yapılandırılmalıdır.");
        }

        var localApplicationData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            throw new InvalidOperationException(
                "Yerel Data Protection anahtar dizini belirlenemedi.");
        }

        return Path.Combine(
            localApplicationData,
            "SecureShop",
            "DataProtection-Keys");
    }
}
