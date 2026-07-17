using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using SecureShop.Mvc.Services.Storage;

namespace SecureShop.Mvc.Tests;

public sealed class ProductImageStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"secureshop-image-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task SaveAsync_WritesValidatedPngInsideSkuFolder()
    {
        Directory.CreateDirectory(_root);
        var storage = new ProductImageStorage(
            new TestWebHostEnvironment(_root));
        await using var stream = new MemoryStream(
        [
            0x89, 0x50, 0x4E, 0x47,
            0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x00
        ]);
        var file = new FormFile(
            stream,
            0,
            stream.Length,
            "Images",
            "image.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        var result = await storage.SaveAsync(
            [file],
            "TEST-SKU",
            "Test Product");

        Assert.True(result.Succeeded);
        Assert.Single(result.Images);
        Assert.StartsWith(
            "/images/products/TEST-SKU/",
            result.Images[0].ImageUrl);
        Assert.Single(Directory.GetFiles(
            Path.Combine(
                _root,
                "images",
                "products",
                "TEST-SKU")));
    }

    [Fact]
    public async Task SaveAsync_RejectsFakePng()
    {
        Directory.CreateDirectory(_root);
        var storage = new ProductImageStorage(
            new TestWebHostEnvironment(_root));
        await using var stream = new MemoryStream(
            "not-a-png"u8.ToArray());
        var file = new FormFile(
            stream,
            0,
            stream.Length,
            "Images",
            "image.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        var result = await storage.SaveAsync(
            [file],
            "TEST-SKU",
            "Test Product");

        Assert.False(result.Succeeded);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private sealed class TestWebHostEnvironment
        : IWebHostEnvironment
    {
        public TestWebHostEnvironment(string webRootPath)
        {
            WebRootPath = webRootPath;
            ContentRootPath = webRootPath;
        }

        public string ApplicationName { get; set; } =
            "SecureShop.Mvc.Tests";
        public IFileProvider WebRootFileProvider { get; set; } =
            new NullFileProvider();
        public string WebRootPath { get; set; }
        public string EnvironmentName { get; set; } =
            "Testing";
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}
