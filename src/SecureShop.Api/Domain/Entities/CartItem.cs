namespace SecureShop.Api.Domain.Entities;

public sealed class CartItem
{
    private const int MaximumQuantity = 99;

    private CartItem()
    {
    }

    internal CartItem(
        Guid cartId,
        Guid productId,
        int quantity)
    {
        if (cartId == Guid.Empty)
        {
            throw new ArgumentException(
                "Sepet kimliği boş olamaz.",
                nameof(cartId));
        }

        if (productId == Guid.Empty)
        {
            throw new ArgumentException(
                "Ürün kimliği boş olamaz.",
                nameof(productId));
        }

        Id = Guid.NewGuid();
        CartId = cartId;
        ProductId = productId;
        SetQuantity(quantity);
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid CartId { get; private set; }

    public Guid ProductId { get; private set; }

    public int Quantity { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public Cart Cart { get; private set; } = null!;

    public Product Product { get; private set; } = null!;

    public void SetQuantity(int quantity)
    {
        if (quantity is < 1 or > MaximumQuantity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                $"Sepet miktarı 1 ile {MaximumQuantity} arasında olmalıdır.");
        }

        Quantity = quantity;
    }
}
