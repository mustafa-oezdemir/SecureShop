namespace SecureShop.Api.Features.QrCodes;

public sealed class OrderQrOptions
{
    public const string SectionName = "QrCodes:Orders";

    public string VerificationBaseUrl { get; set; } =
        "https://localhost:7002/employee/orders/verify";

    public int LifetimeMinutes { get; set; } = 15;
}
