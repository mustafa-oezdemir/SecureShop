namespace SecureShop.Mvc.Models.Requests;

public sealed record CreateProductRequest(
    Guid CategoryId,
    string Name,
    string Sku,
    string? Description,
    decimal Price,
    int StockQuantity,
    IReadOnlyList<CreateProductImageRequest> Images);
