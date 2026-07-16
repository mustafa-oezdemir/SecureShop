namespace SecureShop.Mvc.Models.Requests;

public sealed record CreateProductImageRequest(
    string ImageUrl,
    string AltText);
