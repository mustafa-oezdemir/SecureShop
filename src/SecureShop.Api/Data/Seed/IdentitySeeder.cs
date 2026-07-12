using Microsoft.AspNetCore.Identity;
using SecureShop.Api.Domain.Constants;
using SecureShop.Api.Security.Identity;

namespace SecureShop.Api.Data.Seed;

public sealed class IdentitySeeder
{
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<IdentitySeeder> _logger;

    public IdentitySeeder(
        RoleManager<IdentityRole<Guid>> roleManager,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<IdentitySeeder> logger)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        await EnsureRolesAsync();

        if (!_environment.IsDevelopment())
        {
            return;
        }

        await EnsureDevelopmentUserAsync(
            sectionName: "SeedUsers:Admin",
            roleName: AppRoles.Admin,
            defaultFirstName: "System",
            defaultLastName: "Administrator");

        await EnsureDevelopmentUserAsync(
            sectionName: "SeedUsers:Employee",
            roleName: AppRoles.Employee,
            defaultFirstName: "Development",
            defaultLastName: "Employee");
    }

    private async Task EnsureRolesAsync()
    {
        foreach (var roleName in AppRoles.All)
        {
            if (await _roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var result = await _roleManager.CreateAsync(
                new IdentityRole<Guid>(roleName));

            ThrowIfFailed(
                result,
                $"Role '{roleName}' could not be created.");

            _logger.LogInformation(
                "Identity role created: {RoleName}",
                roleName);
        }
    }

    private async Task EnsureDevelopmentUserAsync(
        string sectionName,
        string roleName,
        string defaultFirstName,
        string defaultLastName)
    {
        var email = _configuration[$"{sectionName}:Email"]?.Trim();
        var password = _configuration[$"{sectionName}:Password"];
        var firstName = GetConfiguredName(
            $"{sectionName}:FirstName",
            defaultFirstName);
        var lastName = GetConfiguredName(
            $"{sectionName}:LastName",
            defaultLastName);

        if (string.IsNullOrWhiteSpace(email)
            && string.IsNullOrWhiteSpace(password))
        {
            _logger.LogInformation(
                "{RoleName} development user was skipped because it is not configured.",
                roleName);

            return;
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException(
                $"{sectionName}:Email configuration was not found.");
        }

        var user = await _userManager.FindByEmailAsync(email);

        if (user is null)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    $"{sectionName}:Password configuration was not found.");
            }

            user = new ApplicationUser(
                email,
                firstName,
                lastName)
            {
                EmailConfirmed = true
            };

            var createResult = await _userManager.CreateAsync(
                user,
                password);

            ThrowIfFailed(
                createResult,
                $"{roleName} development user could not be created.");

            _logger.LogInformation(
                "{RoleName} development user created.",
                roleName);
        }

        if (await _userManager.IsInRoleAsync(user, roleName))
        {
            return;
        }

        var addToRoleResult = await _userManager.AddToRoleAsync(
            user,
            roleName);

        ThrowIfFailed(
            addToRoleResult,
            $"Development user could not be added to role '{roleName}'.");

        _logger.LogInformation(
            "Development user added to {RoleName} role.",
            roleName);
    }

    private string GetConfiguredName(
        string configurationKey,
        string defaultValue)
    {
        var configuredValue = _configuration[configurationKey]?.Trim();

        return string.IsNullOrWhiteSpace(configuredValue)
            ? defaultValue
            : configuredValue;
    }

    private static void ThrowIfFailed(
        IdentityResult result,
        string message)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join(
            "; ",
            result.Errors.Select(error =>
                $"{error.Code}: {error.Description}"));

        throw new InvalidOperationException(
            $"{message} {errors}");
    }
}
