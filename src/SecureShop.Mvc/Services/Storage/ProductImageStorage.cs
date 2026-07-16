using Microsoft.AspNetCore.Http;
using SecureShop.Mvc.Models.Requests;
using SecureShop.Mvc.Services.Interfaces;

namespace SecureShop.Mvc.Services.Storage;

public sealed class ProductImageStorage : IProductImageStorage
{
    private const int MaximumImageCount = 10;
    private const long MaximumImageSize = 5 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, string> AllowedExtensions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".webp"] = "image/webp"
        };

    private readonly IWebHostEnvironment _environment;

    public ProductImageStorage(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<ProductImageStorageResult> SaveAsync(
        IReadOnlyList<IFormFile> files,
        string folderName,
        string productName,
        CancellationToken cancellationToken = default)
    {
        if (files.Count is < 1 or > MaximumImageCount)
        {
            return ProductImageStorageResult.Failure(
                $"1 ile {MaximumImageCount} arasında fotoğraf seçin.");
        }

        var normalizedFolderName = folderName.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(normalizedFolderName)
            || normalizedFolderName.Any(character =>
                !char.IsLetterOrDigit(character)
                && character is not '-' and not '_' and not '.'))
        {
            return ProductImageStorageResult.Failure(
                "Ürün görsel klasörü için SKU geçersiz.");
        }

        var productsRoot = Path.GetFullPath(Path.Combine(
            _environment.WebRootPath,
            "images",
            "products"));
        var productDirectory = Path.GetFullPath(Path.Combine(
            productsRoot,
            normalizedFolderName));

        EnsureInsideRoot(productsRoot, productDirectory);

        if (Directory.Exists(productDirectory))
        {
            return ProductImageStorageResult.Failure(
                "Bu SKU için görsel klasörü zaten bulunuyor.");
        }

        Directory.CreateDirectory(productDirectory);

        try
        {
            var images = new List<CreateProductImageRequest>(files.Count);

            for (var index = 0; index < files.Count; index++)
            {
                var file = files[index];
                var extension = Path.GetExtension(file.FileName)
                    .ToLowerInvariant();

                if (!AllowedExtensions.TryGetValue(
                    extension,
                    out var expectedContentType)
                    || !string.Equals(
                        file.ContentType,
                        expectedContentType,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Yalnızca PNG, JPEG veya WebP fotoğrafları yüklenebilir.");
                }

                if (file.Length is <= 0 or > MaximumImageSize)
                {
                    throw new InvalidOperationException(
                        "Her fotoğraf en fazla 5 MB olabilir.");
                }

                if (!await HasValidSignatureAsync(
                    file,
                    extension,
                    cancellationToken))
                {
                    throw new InvalidOperationException(
                        "Fotoğraf dosyasının içeriği uzantısıyla eşleşmiyor.");
                }

                var fileName = $"{index + 1:D2}-{Guid.NewGuid():N}{extension}";
                var destinationPath = Path.Combine(
                    productDirectory,
                    fileName);

                await using var destination = new FileStream(
                    destinationPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    useAsync: true);

                await file.CopyToAsync(destination, cancellationToken);

                images.Add(new CreateProductImageRequest(
                    $"/images/products/{normalizedFolderName}/{fileName}",
                    $"{productName.Trim()} - görünüm {index + 1}"));
            }

            return ProductImageStorageResult.Success(
                normalizedFolderName,
                images);
        }
        catch (Exception exception)
            when (exception is InvalidOperationException
                or IOException
                or UnauthorizedAccessException)
        {
            if (Directory.Exists(productDirectory))
            {
                Directory.Delete(productDirectory, recursive: true);
            }

            return ProductImageStorageResult.Failure(exception.Message);
        }
    }

    public Task DeleteAsync(
        string folderName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var productsRoot = Path.GetFullPath(Path.Combine(
            _environment.WebRootPath,
            "images",
            "products"));
        var productDirectory = Path.GetFullPath(Path.Combine(
            productsRoot,
            folderName));

        EnsureInsideRoot(productsRoot, productDirectory);

        if (Directory.Exists(productDirectory))
        {
            Directory.Delete(productDirectory, recursive: true);
        }

        return Task.CompletedTask;
    }

    private static async Task<bool> HasValidSignatureAsync(
        IFormFile file,
        string extension,
        CancellationToken cancellationToken)
    {
        var header = new byte[12];
        await using var stream = file.OpenReadStream();
        var bytesRead = await stream.ReadAsync(
            header.AsMemory(0, header.Length),
            cancellationToken);

        return extension switch
        {
            ".png" => bytesRead >= 8
                && header.AsSpan(0, 8).SequenceEqual(
                    new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            ".jpg" or ".jpeg" => bytesRead >= 3
                && header[0] == 0xFF
                && header[1] == 0xD8
                && header[2] == 0xFF,
            ".webp" => bytesRead >= 12
                && header.AsSpan(0, 4).SequenceEqual("RIFF"u8)
                && header.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };
    }

    private static void EnsureInsideRoot(
        string root,
        string candidate)
    {
        var rootWithSeparator = root.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(
            rootWithSeparator,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Ürün görsel klasörü geçersiz.");
        }
    }
}
