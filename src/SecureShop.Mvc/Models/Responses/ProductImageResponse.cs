namespace SecureShop.Mvc.Models.Responses;

public sealed record ProductImageResponse(
    Guid Id,
    string ImageUrl,
    string AltText,
    int SortOrder,
    bool IsPrimary);
