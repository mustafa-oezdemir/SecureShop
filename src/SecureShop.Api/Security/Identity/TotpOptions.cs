namespace SecureShop.Api.Security.Identity;

public sealed class TotpOptions
{
    public const string SectionName = "Authentication:Totp";

    public string Issuer { get; set; } = "SecureShop";
}
