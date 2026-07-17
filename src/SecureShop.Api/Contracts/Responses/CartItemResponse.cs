namespace SecureShop.Api.Contracts.Responses;

public sealed record CartItemResponse(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string Sku,
    string? ImageUrl,
    string ImageAltText,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal,
    int AvailableStock,
    bool IsAvailable);
