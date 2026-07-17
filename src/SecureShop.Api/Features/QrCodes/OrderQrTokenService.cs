using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace SecureShop.Api.Features.QrCodes;

public sealed class OrderQrTokenService : IOrderQrTokenService
{
    private readonly ITimeLimitedDataProtector _protector;
    private readonly TimeSpan _lifetime;

    public OrderQrTokenService(
        IDataProtectionProvider dataProtectionProvider,
        IOptions<OrderQrOptions> options)
    {
        _protector = dataProtectionProvider
            .CreateProtector(
                "SecureShop.Orders.QrVerification.v1")
            .ToTimeLimitedDataProtector();

        _lifetime = TimeSpan.FromMinutes(
            options.Value.LifetimeMinutes);
    }

    public string Generate(Guid orderId)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException(
                "Sipariş kimliği boş olamaz.",
                nameof(orderId));
        }

        return _protector.Protect(
            orderId.ToString("N"),
            _lifetime);
    }

    public bool TryValidate(string token, out Guid orderId)
    {
        orderId = Guid.Empty;

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            var value = _protector.Unprotect(
                token.Trim(),
                out _);

            return Guid.TryParseExact(value, "N", out orderId);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
