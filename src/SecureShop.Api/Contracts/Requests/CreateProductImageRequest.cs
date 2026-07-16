using System.ComponentModel.DataAnnotations;

namespace SecureShop.Api.Contracts.Requests;

public sealed class CreateProductImageRequest
{
    [Required]
    [StringLength(500)]
    [RegularExpression(
        "^/images/products/[A-Za-z0-9._-]+/[A-Za-z0-9._-]+\\.(png|jpg|jpeg|webp)$",
        ErrorMessage = "Ürün görsel adresi geçersiz.")]
    public string ImageUrl { get; init; } = string.Empty;

    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string AltText { get; init; } = string.Empty;
}
