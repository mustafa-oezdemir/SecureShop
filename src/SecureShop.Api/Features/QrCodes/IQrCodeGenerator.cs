namespace SecureShop.Api.Features.QrCodes;

public interface IQrCodeGenerator
{
    string GeneratePngDataUrl(string content);
}
