namespace SecureShop.Api.Domain.Entities;

public sealed class ProductImage
{
    private ProductImage()
    {
    }

    public ProductImage(
        Guid productId,
        string imageUrl,
        string altText,
        int sortOrder,
        bool isPrimary = false)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException(
                "Ürün kimliği boş olamaz.",
                nameof(productId));
        }

        Id = Guid.NewGuid();
        ProductId = productId;
        SetImageUrl(imageUrl);
        SetAltText(altText);
        SetSortOrder(sortOrder);
        IsPrimary = isPrimary;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid ProductId { get; private set; }

    public string ImageUrl { get; private set; } = string.Empty;

    public string AltText { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    public bool IsPrimary { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public Product Product { get; private set; } = null!;

    private void SetImageUrl(string imageUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imageUrl);

        var normalizedUrl = imageUrl.Trim();

        if (normalizedUrl.Length > 500)
        {
            throw new ArgumentOutOfRangeException(
                nameof(imageUrl),
                "Görsel adresi 500 karakterden uzun olamaz.");
        }

        ImageUrl = normalizedUrl;
    }

    private void SetAltText(string altText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(altText);

        var normalizedAltText = altText.Trim();

        if (normalizedAltText.Length > 200)
        {
            throw new ArgumentOutOfRangeException(
                nameof(altText),
                "Görsel açıklaması 200 karakterden uzun olamaz.");
        }

        AltText = normalizedAltText;
    }

    private void SetSortOrder(int sortOrder)
    {
        if (sortOrder is < 0 or > 99)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sortOrder),
                "Görsel sırası 0 ile 99 arasında olmalıdır.");
        }

        SortOrder = sortOrder;
    }
}
