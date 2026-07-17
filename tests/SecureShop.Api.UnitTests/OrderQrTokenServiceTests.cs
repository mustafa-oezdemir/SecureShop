using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using SecureShop.Api.Features.QrCodes;

namespace SecureShop.Api.UnitTests;

public sealed class OrderQrTokenServiceTests : IDisposable
{
    private readonly string _keyDirectory = Path.Combine(
        Path.GetTempPath(),
        $"secureshop-qr-tests-{Guid.NewGuid():N}");

    [Fact]
    public void GenerateAndValidate_RoundTripsOrderId()
    {
        Directory.CreateDirectory(_keyDirectory);
        var provider = DataProtectionProvider.Create(
            new DirectoryInfo(_keyDirectory));
        var service = new OrderQrTokenService(
            provider,
            Options.Create(new OrderQrOptions
            {
                LifetimeMinutes = 30
            }));
        var orderId = Guid.NewGuid();

        var token = service.Generate(orderId);
        var isValid = service.TryValidate(
            token,
            out var validatedOrderId);

        Assert.True(isValid);
        Assert.Equal(orderId, validatedOrderId);
    }

    [Fact]
    public void TryValidate_RejectsTamperedToken()
    {
        Directory.CreateDirectory(_keyDirectory);
        var provider = DataProtectionProvider.Create(
            new DirectoryInfo(_keyDirectory));
        var service = new OrderQrTokenService(
            provider,
            Options.Create(new OrderQrOptions
            {
                LifetimeMinutes = 30
            }));

        var isValid = service.TryValidate(
            "tampered-token",
            out var orderId);

        Assert.False(isValid);
        Assert.Equal(Guid.Empty, orderId);
    }

    public void Dispose()
    {
        if (Directory.Exists(_keyDirectory))
        {
            Directory.Delete(_keyDirectory, recursive: true);
        }
    }
}
