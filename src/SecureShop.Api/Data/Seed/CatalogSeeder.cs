using Microsoft.EntityFrameworkCore;
using SecureShop.Api.Domain.Entities;

namespace SecureShop.Api.Data.Seed;

public sealed class CatalogSeeder
{
    private static readonly IReadOnlyList<ProductSeedDefinition> Products =
    [
        new(
            "Kablosuz Gürültü Engelleyici Kulaklık",
            "SSH-HEADPHONE-01",
            "Aktif gürültü engelleme, şeffaf mod ve 40 saate kadar pil ömrü sunan premium kablosuz kulaklık.",
            249.90m,
            24,
            "headphones",
            5),
        new(
            "Akıllı Spor Saati",
            "SSH-WATCH-01",
            "AMOLED ekran, GPS, sağlık takibi ve suya dayanıklı alüminyum gövdeli akıllı saat.",
            189.90m,
            31,
            "smartwatch",
            0),
        new(
            "RGB Mekanik Klavye",
            "SSH-KEYBOARD-01",
            "Hot-swap mekanik switch, RGB aydınlatma ve kompakt alüminyum kasaya sahip oyuncu klavyesi.",
            139.90m,
            18,
            "keyboard",
            0),
        new(
            "Taşınabilir Bluetooth Hoparlör",
            "SSH-SPEAKER-01",
            "360 derece ses, IP67 koruma ve 18 saat pil ömrü sunan taşınabilir Bluetooth hoparlör.",
            119.90m,
            42,
            "speaker",
            0),
        new(
            "4K Aksiyon Kamerası",
            "SSH-CAMERA-01",
            "4K video, gelişmiş görüntü sabitleme ve su geçirmez gövdeye sahip kompakt aksiyon kamerası.",
            329.90m,
            15,
            "SSH-CAMERA-01",
            4)
    ];

    private readonly AppDbContext _dbContext;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<CatalogSeeder> _logger;

    public CatalogSeeder(
        AppDbContext dbContext,
        IHostEnvironment environment,
        ILogger<CatalogSeeder> logger)
    {
        _dbContext = dbContext;
        _environment = environment;
        _logger = logger;
    }

    public async Task SeedAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_environment.IsDevelopment())
        {
            return;
        }

        var category = await _dbContext.Categories
            .SingleOrDefaultAsync(
                item => item.Name == "Elektronik",
                cancellationToken);

        if (category is null)
        {
            category = new Category("Elektronik");
            _dbContext.Categories.Add(category);
        }

        foreach (var definition in Products)
        {
            var product = await _dbContext.Products
                .Include(item => item.Images)
                .SingleOrDefaultAsync(
                    item => item.Sku == definition.Sku,
                    cancellationToken);

            if (product is null)
            {
                product = new Product(
                    category.Id,
                    definition.Name,
                    definition.Sku,
                    definition.Price,
                    definition.StockQuantity,
                    definition.Description);

                _dbContext.Products.Add(product);
            }

            var expectedImageUrls = Enumerable
                .Range(1, definition.ImageCount)
                .Select(index =>
                    $"/images/products/{definition.AssetPrefix}/{index}.png")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var obsoleteImage in product.Images
                .Where(image => !expectedImageUrls.Contains(image.ImageUrl))
                .ToList())
            {
                product.Images.Remove(obsoleteImage);
                _dbContext.ProductImages.Remove(obsoleteImage);
            }

            for (var index = 1; index <= definition.ImageCount; index++)
            {
                var sortOrder = index - 1;

                if (product.Images.Any(
                    image => image.SortOrder == sortOrder))
                {
                    continue;
                }

                product.AddImage(
                    $"/images/products/{definition.AssetPrefix}/{index}.png",
                    $"{definition.Name} - görünüm {index}",
                    sortOrder,
                    isPrimary: index == 1);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Development catalog synchronized with {ProductCount} products and {ImageCount} images.",
            Products.Count,
            Products.Sum(product => product.ImageCount));
    }

    private sealed record ProductSeedDefinition(
        string Name,
        string Sku,
        string Description,
        decimal Price,
        int StockQuantity,
        string AssetPrefix,
        int ImageCount);
}
