namespace SecureShop.Mvc.Models.Requests;

public sealed record UpdateProductRequest(
    Guid CategoryId,
    string Name,
    string Sku,
    string? Description,
    decimal Price,
    int StockQuantity,
    string RowVersion);
