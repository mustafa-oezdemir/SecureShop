namespace SecureShop.Mvc.Models.Responses;

public sealed record CartResponse(
    Guid? Id,
    IReadOnlyList<CartItemResponse> Items,
    int TotalQuantity,
    decimal TotalAmount,
    DateTimeOffset? UpdatedAtUtc);
