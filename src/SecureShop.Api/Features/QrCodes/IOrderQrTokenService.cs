namespace SecureShop.Api.Features.QrCodes;

public interface IOrderQrTokenService
{
    string Generate(Guid orderId);

    bool TryValidate(string token, out Guid orderId);
}
