namespace SecureShop.Api.Domain.Entities;

public sealed class Product
{
    private Product()
    {
    }

    public Product(
        Guid categoryId,
        string name,
        string sku,
        decimal price,
        int stockQuantity,
        string? description = null)
    {
        if (categoryId == Guid.Empty)
        {
            throw new ArgumentException(
                "Kategori kimliği boş olamaz.",
                nameof(categoryId));
        }

        Id = Guid.NewGuid();
        CategoryId = categoryId;

        SetName(name);
        SetSku(sku);
        SetDescription(description);
        SetPrice(price);
        SetStockQuantity(stockQuantity);

        IsActive = true;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid CategoryId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Sku { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public decimal Price { get; private set; }

    public int StockQuantity { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public Category Category { get; private set; } = null!;

    public ICollection<ProductImage> Images { get; private set; } =
        new List<ProductImage>();

    public void AddImage(
        string imageUrl,
        string altText,
        int sortOrder,
        bool isPrimary = false)
    {
        if (Images.Any(image => image.SortOrder == sortOrder))
        {
            throw new InvalidOperationException(
                "Aynı görsel sırası bir ürün içinde tekrar kullanılamaz.");
        }

        if (isPrimary && Images.Any(image => image.IsPrimary))
        {
            throw new InvalidOperationException(
                "Bir ürünün yalnızca bir ana görseli olabilir.");
        }

        Images.Add(new ProductImage(
            Id,
            imageUrl,
            altText,
            sortOrder,
            isPrimary));

        MarkAsUpdated();
    }

    public void SetName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var normalizedName = name.Trim();

        if (normalizedName.Length > 200)
        {
            throw new ArgumentOutOfRangeException(
                nameof(name),
                "Ürün adı 200 karakterden uzun olamaz.");
        }

        Name = normalizedName;
        MarkAsUpdated();
    }

    public void SetSku(string sku)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);

        var normalizedSku = sku.Trim().ToUpperInvariant();

        if (normalizedSku.Length > 64)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sku),
                "SKU 64 karakterden uzun olamaz.");
        }

        Sku = normalizedSku;
        MarkAsUpdated();
    }

    public void SetDescription(string? description)
    {
        var normalizedDescription = string.IsNullOrWhiteSpace(description)
            ? null
            : description.Trim();

        if (normalizedDescription?.Length > 2000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(description),
                "Ürün açıklaması 2000 karakterden uzun olamaz.");
        }

        Description = normalizedDescription;
        MarkAsUpdated();
    }

    public void SetPrice(decimal price)
    {
        if (price < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(price),
                "Ürün fiyatı negatif olamaz.");
        }

        Price = decimal.Round(
            price,
            decimals: 2,
            mode: MidpointRounding.ToEven);

        MarkAsUpdated();
    }

    public void SetStockQuantity(int stockQuantity)
    {
        if (stockQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stockQuantity),
                "Stok miktarı negatif olamaz.");
        }

        StockQuantity = stockQuantity;
        MarkAsUpdated();
    }

    public void DecreaseStock(int quantity)
    {
        if (quantity < 1 || quantity > StockQuantity)
        {
            throw new InvalidOperationException(
                "Ürün stoğu sipariş miktarı için yetersiz.");
        }

        StockQuantity -= quantity;
        MarkAsUpdated();
    }

    public void IncreaseStock(int quantity)
    {
        if (quantity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        checked
        {
            StockQuantity += quantity;
        }

        MarkAsUpdated();
    }

    public void ChangeCategory(Guid categoryId)
    {
        if (categoryId == Guid.Empty)
        {
            throw new ArgumentException(
                "Kategori kimliği boş olamaz.",
                nameof(categoryId));
        }

        CategoryId = categoryId;
        MarkAsUpdated();
    }

    public void Activate()
    {
        IsActive = true;
        MarkAsUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        MarkAsUpdated();
    }

    private void MarkAsUpdated()
    {
        if (CreatedAtUtc != default)
        {
            UpdatedAtUtc = DateTimeOffset.UtcNow;
        }
    }
}
