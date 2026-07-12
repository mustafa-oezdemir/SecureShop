using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SecureShop.Api.Contracts.Responses;
using SecureShop.Api.Features.QrCodes;
using SecureShop.Api.Security.Identity;

namespace SecureShop.Api.Features.Auth.TwoFactor;

public sealed class AuthenticatorEnrollmentService
    : IAuthenticatorEnrollmentService
{
    private const int TotpDigits = 6;
    private const int TotpPeriodSeconds = 30;
    private const int RecoveryCodeCount = 10;

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IQrCodeGenerator _qrCodeGenerator;
    private readonly TotpOptions _totpOptions;

    public AuthenticatorEnrollmentService(
        UserManager<ApplicationUser> userManager,
        IQrCodeGenerator qrCodeGenerator,
        IOptions<TotpOptions> totpOptions)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(qrCodeGenerator);
        ArgumentNullException.ThrowIfNull(totpOptions);

        _userManager = userManager;
        _qrCodeGenerator = qrCodeGenerator;
        _totpOptions = totpOptions.Value;
    }

    public async Task<AuthenticatorSetupResponse> CreateSetupAsync(
        ApplicationUser user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        cancellationToken.ThrowIfCancellationRequested();

        var authenticatorKey =
            await _userManager.GetAuthenticatorKeyAsync(user);

        if (string.IsNullOrWhiteSpace(authenticatorKey))
        {
            var resetResult =
                await _userManager.ResetAuthenticatorKeyAsync(user);

            ThrowIfFailed(
                resetResult,
                "Authenticator anahtari olusturulamadi.");

            authenticatorKey =
                await _userManager.GetAuthenticatorKeyAsync(user);
        }

        if (string.IsNullOrWhiteSpace(authenticatorKey))
        {
            throw new InvalidOperationException(
                "Authenticator anahtari alinamadi.");
        }

        var accountName =
            user.Email
            ?? user.UserName
            ?? throw new InvalidOperationException(
                "Kullanicinin e-posta veya kullanici adi bulunamadi.");

        var authenticatorUri = GenerateAuthenticatorUri(
            accountName,
            authenticatorKey);

        var qrCodeImageDataUrl =
            _qrCodeGenerator.GeneratePngDataUrl(
                authenticatorUri);

        return new AuthenticatorSetupResponse(
            SharedKey: FormatSharedKey(authenticatorKey),
            AuthenticatorUri: authenticatorUri,
            QrCodeImageDataUrl: qrCodeImageDataUrl,
            Digits: TotpDigits,
            PeriodSeconds: TotpPeriodSeconds);
    }

    public async Task<EnableAuthenticatorResponse?> EnableAsync(
        ApplicationUser user,
        string verificationCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(verificationCode))
        {
            return null;
        }

        var normalizedCode =
            NormalizeVerificationCode(verificationCode);

        if (!IsValidCodeFormat(normalizedCode))
        {
            return null;
        }

        var isValid = await _userManager.VerifyTwoFactorTokenAsync(
            user,
            TokenOptions.DefaultAuthenticatorProvider,
            normalizedCode);

        if (!isValid)
        {
            return null;
        }

        var enableResult =
            await _userManager.SetTwoFactorEnabledAsync(
                user,
                enabled: true);

        ThrowIfFailed(
            enableResult,
            "Iki faktorlu kimlik dogrulama etkinlestirilemedi.");

        var recoveryCodes =
            await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(
                user,
                RecoveryCodeCount);

        if (recoveryCodes is null)
        {
            throw new InvalidOperationException(
                "Recovery code degerleri olusturulamadi.");
        }

        var securityStampResult =
            await _userManager.UpdateSecurityStampAsync(user);

        ThrowIfFailed(
            securityStampResult,
            "Kullanicinin guvenlik damgasi guncellenemedi.");

        cancellationToken.ThrowIfCancellationRequested();

        return new EnableAuthenticatorResponse(
            IsEnabled: true,
            RecoveryCodes: recoveryCodes.ToArray());
    }

    private string GenerateAuthenticatorUri(
        string accountName,
        string authenticatorKey)
    {
        var issuer = _totpOptions.Issuer.Trim();

        var encodedIssuer =
            UrlEncoder.Default.Encode(issuer);

        var encodedAccountName =
            UrlEncoder.Default.Encode(accountName.Trim());

        var encodedKey =
            UrlEncoder.Default.Encode(authenticatorKey);

        return
            $"otpauth://totp/{encodedIssuer}:{encodedAccountName}" +
            $"?secret={encodedKey}" +
            $"&issuer={encodedIssuer}" +
            "&algorithm=SHA1" +
            $"&digits={TotpDigits}" +
            $"&period={TotpPeriodSeconds}";
    }

    private static string FormatSharedKey(
        string authenticatorKey)
    {
        var groups = authenticatorKey
            .ToLowerInvariant()
            .Chunk(4)
            .Select(group => new string(group));

        return string.Join(" ", groups);
    }

    private static string NormalizeVerificationCode(
        string verificationCode)
    {
        return verificationCode
            .Replace(
                " ",
                string.Empty,
                StringComparison.Ordinal)
            .Replace(
                "-",
                string.Empty,
                StringComparison.Ordinal)
            .Trim();
    }

    private static bool IsValidCodeFormat(
        string verificationCode)
    {
        return verificationCode.Length == TotpDigits
            && verificationCode.All(char.IsDigit);
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
