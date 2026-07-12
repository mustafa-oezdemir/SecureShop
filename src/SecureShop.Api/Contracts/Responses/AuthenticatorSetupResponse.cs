namespace SecureShop.Api.Contracts.Responses;

public sealed record AuthenticatorSetupResponse(
    string SharedKey,
    string AuthenticatorUri,
    string QrCodeImageDataUrl,
    int Digits,
    int PeriodSeconds);
