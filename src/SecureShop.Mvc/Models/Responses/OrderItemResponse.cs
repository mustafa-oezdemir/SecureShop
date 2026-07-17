namespace SecureShop.Mvc.Models.Responses;

public sealed record OrderItemResponse(
    Guid ProductId,
    string ProductName,
    string Sku,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);
