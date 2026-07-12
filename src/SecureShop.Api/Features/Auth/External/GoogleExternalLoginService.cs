using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SecureShop.Api.Data;
using SecureShop.Api.Domain.Constants;
using SecureShop.Api.Security.Identity;

namespace SecureShop.Api.Features.Auth.External;

public sealed class GoogleExternalLoginService
    : IGoogleExternalLoginService
{
    private const int MaximumNameLength = 100;

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly AppDbContext _dbContext;

    public GoogleExternalLoginService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        AppDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(signInManager);
        ArgumentNullException.ThrowIfNull(dbContext);

        _userManager = userManager;
        _signInManager = signInManager;
        _dbContext = dbContext;
    }

    public async Task<GoogleExternalLoginResult> CompleteAsync(
        ExternalLoginInfo externalLoginInfo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(externalLoginInfo);

        cancellationToken.ThrowIfCancellationRequested();

        var linkedUser = await _userManager.FindByLoginAsync(
            externalLoginInfo.LoginProvider,
            externalLoginInfo.ProviderKey);

        if (linkedUser is not null)
        {
            return await SignInExistingUserAsync(
                linkedUser,
                externalLoginInfo);
        }

        var email = GetClaimValue(
            externalLoginInfo.Principal,
            ClaimTypes.Email,
            "email");

        if (string.IsNullOrWhiteSpace(email))
        {
            return new GoogleExternalLoginResult(
                GoogleExternalLoginStatus.EmailNotAvailable);
        }

        if (!IsEmailVerified(externalLoginInfo.Principal))
        {
            return new GoogleExternalLoginResult(
                GoogleExternalLoginStatus.EmailNotVerified);
        }

        email = email.Trim();

        var existingEmailUser =
            await _userManager.FindByEmailAsync(email);

        if (existingEmailUser is not null)
        {
            return new GoogleExternalLoginResult(
                GoogleExternalLoginStatus.EmailAlreadyRegistered);
        }

        return await CreateAndSignInUserAsync(
            externalLoginInfo,
            email,
            cancellationToken);
    }

    private async Task<GoogleExternalLoginResult>
        SignInExistingUserAsync(
            ApplicationUser user,
            ExternalLoginInfo externalLoginInfo)
    {
        if (!user.IsActive)
        {
            return new GoogleExternalLoginResult(
                GoogleExternalLoginStatus.Inactive);
        }

        var signInResult =
            await _signInManager.ExternalLoginSignInAsync(
                externalLoginInfo.LoginProvider,
                externalLoginInfo.ProviderKey,
                isPersistent: false,
                bypassTwoFactor: false);

        if (signInResult.RequiresTwoFactor)
        {
            return new GoogleExternalLoginResult(
                GoogleExternalLoginStatus.RequiresTwoFactor);
        }

        if (signInResult.IsLockedOut)
        {
            return new GoogleExternalLoginResult(
                GoogleExternalLoginStatus.LockedOut);
        }

        if (signInResult.IsNotAllowed)
        {
            return new GoogleExternalLoginResult(
                GoogleExternalLoginStatus.NotAllowed);
        }

        if (!signInResult.Succeeded)
        {
            return new GoogleExternalLoginResult(
                GoogleExternalLoginStatus.Failed);
        }

        return await CreateSucceededResultAsync(user);
    }

    private async Task<GoogleExternalLoginResult>
        CreateAndSignInUserAsync(
            ExternalLoginInfo externalLoginInfo,
            string email,
            CancellationToken cancellationToken)
    {
        var firstName = GetFirstName(
            externalLoginInfo.Principal,
            email);

        var lastName = GetLastName(
            externalLoginInfo.Principal);

        var strategy =
            _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction =
                await _dbContext.Database.BeginTransactionAsync(
                    cancellationToken);

            var user = new ApplicationUser(
                email,
                firstName,
                lastName)
            {
                EmailConfirmed = true,
                LockoutEnabled = true
            };

            var createResult =
                await _userManager.CreateAsync(user);

            EnsureSucceeded(
                createResult,
                "Google kullanicisi olusturulamadi.");

            var addLoginResult =
                await _userManager.AddLoginAsync(
                    user,
                    externalLoginInfo);

            EnsureSucceeded(
                addLoginResult,
                "Google login bilgisi kullaniciya eklenemedi.");

            var addRoleResult =
                await _userManager.AddToRoleAsync(
                    user,
                    AppRoles.Kunde);

            EnsureSucceeded(
                addRoleResult,
                "Google kullanicisi Kunde rolune eklenemedi.");

            await transaction.CommitAsync(cancellationToken);

            await _signInManager.SignInAsync(
                user,
                isPersistent: false,
                authenticationMethod:
                    externalLoginInfo.LoginProvider);

            return await CreateSucceededResultAsync(user);
        });
    }

    private async Task<GoogleExternalLoginResult>
        CreateSucceededResultAsync(
            ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);

        var displayName =
            $"{user.FirstName} {user.LastName}".Trim();

        return new GoogleExternalLoginResult(
            Status: GoogleExternalLoginStatus.Succeeded,
            Email: user.Email,
            DisplayName: displayName,
            Roles: roles.ToArray());
    }

    private static bool IsEmailVerified(
        ClaimsPrincipal principal)
    {
        var verifiedClaim =
            principal.Claims.FirstOrDefault(claim =>
                claim.Type.Equals(
                    "email_verified",
                    StringComparison.OrdinalIgnoreCase)
                || claim.Type.EndsWith(
                    "/email_verified",
                    StringComparison.OrdinalIgnoreCase));

        return verifiedClaim is not null
            && bool.TryParse(
                verifiedClaim.Value,
                out var isVerified)
            && isVerified;
    }

    private static string GetFirstName(
        ClaimsPrincipal principal,
        string email)
    {
        var firstName = GetClaimValue(
            principal,
            ClaimTypes.GivenName,
            "given_name");

        if (!string.IsNullOrWhiteSpace(firstName))
        {
            return LimitLength(firstName.Trim());
        }

        var fullName = GetClaimValue(
            principal,
            ClaimTypes.Name,
            "name");

        if (!string.IsNullOrWhiteSpace(fullName))
        {
            var firstPart = fullName
                .Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(firstPart))
            {
                return LimitLength(firstPart);
            }
        }

        var emailLocalPart =
            email.Split('@', 2)[0];

        return LimitLength(emailLocalPart);
    }

    private static string GetLastName(
        ClaimsPrincipal principal)
    {
        var lastName = GetClaimValue(
            principal,
            ClaimTypes.Surname,
            "family_name");

        if (!string.IsNullOrWhiteSpace(lastName))
        {
            return LimitLength(lastName.Trim());
        }

        var fullName = GetClaimValue(
            principal,
            ClaimTypes.Name,
            "name");

        if (!string.IsNullOrWhiteSpace(fullName))
        {
            var nameParts = fullName.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);

            if (nameParts.Length > 1)
            {
                return LimitLength(
                    string.Join(
                        ' ',
                        nameParts.Skip(1)));
            }
        }

        return "Google";
    }

    private static string? GetClaimValue(
        ClaimsPrincipal principal,
        params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value =
                principal.FindFirst(claimType)?.Value;

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string LimitLength(string value)
    {
        var normalizedValue = value.Trim();

        return normalizedValue.Length <= MaximumNameLength
            ? normalizedValue
            : normalizedValue[..MaximumNameLength];
    }

    private static void EnsureSucceeded(
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
