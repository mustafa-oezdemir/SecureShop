namespace SecureShop.Api.Contracts.Responses;

public sealed record ProductImageResponse(
    Guid Id,
    string ImageUrl,
    string AltText,
    int SortOrder,
    bool IsPrimary);
