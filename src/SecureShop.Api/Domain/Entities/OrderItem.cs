namespace SecureShop.Api.Domain.Entities;

public sealed class OrderItem
{
    private OrderItem()
    {
    }

    internal OrderItem(
        Guid orderId,
        Guid productId,
        string productName,
        string sku,
        decimal unitPrice,
        int quantity)
    {
        if (orderId == Guid.Empty || productId == Guid.Empty)
        {
            throw new ArgumentException(
                "Sipariş ve ürün kimliği boş olamaz.");
        }

        if (quantity is < 1 or > 99)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity));
        }

        if (unitPrice < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(unitPrice));
        }

        Id = Guid.NewGuid();
        OrderId = orderId;
        ProductId = productId;
        ProductName = Normalize(productName, 200, nameof(productName));
        Sku = Normalize(sku, 64, nameof(sku)).ToUpperInvariant();
        UnitPrice = decimal.Round(
            unitPrice,
            2,
            MidpointRounding.ToEven);
        Quantity = quantity;
        LineTotal = decimal.Round(
            UnitPrice * Quantity,
            2,
            MidpointRounding.ToEven);
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public Guid ProductId { get; private set; }

    public string ProductName { get; private set; } = string.Empty;

    public string Sku { get; private set; } = string.Empty;

    public decimal UnitPrice { get; private set; }

    public int Quantity { get; private set; }

    public decimal LineTotal { get; private set; }

    public Order Order { get; private set; } = null!;

    public Product Product { get; private set; } = null!;

    private static string Normalize(
        string value,
        int maximumLength,
        string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            value,
            parameterName);

        var normalized = value.Trim();

        if (normalized.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }

        return normalized;
    }
}
