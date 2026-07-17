using SecureShop.Api.Domain.Enums;

namespace SecureShop.Api.Domain.Entities;

public sealed class Order
{
    private Order()
    {
    }

    public Order(
        Guid userId,
        string orderNumber,
        string recipientName,
        string addressLine,
        string postalCode,
        string city,
        string country)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "Kullanıcı kimliği boş olamaz.",
                nameof(userId));
        }

        Id = Guid.NewGuid();
        UserId = userId;
        OrderNumber = NormalizeRequired(
            orderNumber,
            32,
            nameof(orderNumber));
        RecipientName = NormalizeRequired(
            recipientName,
            200,
            nameof(recipientName));
        AddressLine = NormalizeRequired(
            addressLine,
            500,
            nameof(addressLine));
        PostalCode = NormalizeRequired(
            postalCode,
            20,
            nameof(postalCode));
        City = NormalizeRequired(city, 100, nameof(city));
        Country = NormalizeRequired(country, 100, nameof(country));
        Status = OrderStatus.PendingApproval;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string OrderNumber { get; private set; } = string.Empty;

    public string RecipientName { get; private set; } = string.Empty;

    public string AddressLine { get; private set; } = string.Empty;

    public string PostalCode { get; private set; } = string.Empty;

    public string City { get; private set; } = string.Empty;

    public string Country { get; private set; } = string.Empty;

    public OrderStatus Status { get; private set; }

    public decimal TotalAmount { get; private set; }

    public Guid? ProcessedByUserId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public ICollection<OrderItem> Items { get; private set; } =
        new List<OrderItem>();

    public void AddItem(
        Guid productId,
        string productName,
        string sku,
        decimal unitPrice,
        int quantity)
    {
        if (Status != OrderStatus.PendingApproval)
        {
            throw new InvalidOperationException(
                "Yalnızca bekleyen siparişe ürün eklenebilir.");
        }

        if (Items.Any(item => item.ProductId == productId))
        {
            throw new InvalidOperationException(
                "Aynı ürün siparişe iki kez eklenemez.");
        }

        Items.Add(new OrderItem(
            Id,
            productId,
            productName,
            sku,
            unitPrice,
            quantity));

        RecalculateTotal();
    }

    public void Approve(Guid staffUserId)
    {
        EnsureStaffUser(staffUserId);
        EnsureStatus(OrderStatus.PendingApproval);
        Status = OrderStatus.Approved;
        ProcessedByUserId = staffUserId;
        Touch();
    }

    public void MarkReadyForPickup(Guid staffUserId)
    {
        EnsureStaffUser(staffUserId);
        EnsureStatus(OrderStatus.Approved);
        Status = OrderStatus.ReadyForPickup;
        ProcessedByUserId = staffUserId;
        Touch();
    }

    public void Complete(Guid staffUserId)
    {
        EnsureStaffUser(staffUserId);
        EnsureStatus(OrderStatus.ReadyForPickup);
        Status = OrderStatus.Completed;
        ProcessedByUserId = staffUserId;
        CompletedAtUtc = DateTimeOffset.UtcNow;
        Touch();
    }

    public void Cancel(Guid staffUserId)
    {
        EnsureStaffUser(staffUserId);

        if (Status is not OrderStatus.PendingApproval
            and not OrderStatus.Approved)
        {
            throw new InvalidOperationException(
                "Bu sipariş artık iptal edilemez.");
        }

        Status = OrderStatus.Cancelled;
        ProcessedByUserId = staffUserId;
        Touch();
    }

    private void RecalculateTotal()
    {
        TotalAmount = decimal.Round(
            Items.Sum(item => item.LineTotal),
            2,
            MidpointRounding.ToEven);
    }

    private void EnsureStatus(OrderStatus expected)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException(
                $"Sipariş durumu '{expected}' olmalıdır.");
        }
    }

    private static void EnsureStaffUser(Guid staffUserId)
    {
        if (staffUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "Personel kimliği boş olamaz.",
                nameof(staffUserId));
        }
    }

    private void Touch()
    {
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private static string NormalizeRequired(
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
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"Değer {maximumLength} karakterden uzun olamaz.");
        }

        return normalized;
    }
}
