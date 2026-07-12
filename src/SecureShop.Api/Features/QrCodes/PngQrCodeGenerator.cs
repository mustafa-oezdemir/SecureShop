using QRCoder;

namespace SecureShop.Api.Features.QrCodes;

public sealed class PngQrCodeGenerator : IQrCodeGenerator
{
    private const int PixelsPerModule = 8;

    public string GeneratePngDataUrl(string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        using var qrCodeData =
            QRCodeGenerator.GenerateQrCode(
                content,
                QRCodeGenerator.ECCLevel.Q);

        using var qrCode =
            new PngByteQRCode(qrCodeData);

        var pngBytes = qrCode.GetGraphic(
            PixelsPerModule,
            drawQuietZones: true);

        return $"data:image/png;base64,{Convert.ToBase64String(pngBytes)}";
    }
}
