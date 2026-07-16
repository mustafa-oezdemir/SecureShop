namespace SecureShop.Api.Domain.Entities;

public sealed class Cart
{
    private Cart()
    {
    }

    public Cart(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "Kullanıcı kimliği boş olamaz.",
                nameof(userId));
        }

        Id = Guid.NewGuid();
        UserId = userId;
        CreatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public ICollection<CartItem> Items { get; private set; } =
        new List<CartItem>();

    public CartItem AddItem(Guid productId, int quantity)
    {
        var existingItem = Items.SingleOrDefault(
            item => item.ProductId == productId);

        if (existingItem is not null)
        {
            existingItem.SetQuantity(existingItem.Quantity + quantity);
            Touch();
            return existingItem;
        }

        var item = new CartItem(Id, productId, quantity);
        Items.Add(item);
        Touch();
        return item;
    }

    public void SetItemQuantity(CartItem item, int quantity)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.CartId != Id)
        {
            throw new InvalidOperationException(
                "Sepet öğesi bu sepete ait değil.");
        }

        item.SetQuantity(quantity);
        Touch();
    }

    public void RemoveItem(CartItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!Items.Remove(item))
        {
            throw new InvalidOperationException(
                "Sepet öğesi bu sepete ait değil.");
        }

        Touch();
    }

    public void Clear()
    {
        Items.Clear();
        Touch();
    }

    private void Touch()
    {
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}
