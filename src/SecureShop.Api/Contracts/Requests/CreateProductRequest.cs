using System.ComponentModel.DataAnnotations;

namespace SecureShop.Api.Contracts.Requests;

public sealed class CreateProductRequest
{
    [Required]
    public Guid CategoryId { get; init; }

    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [StringLength(64, MinimumLength = 1)]
    [RegularExpression("^[A-Za-z0-9._-]+$")]
    public string Sku { get; init; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; init; }

    [Range(typeof(decimal), "0", "9999999999999999.99")]
    public decimal Price { get; init; }

    [Range(0, int.MaxValue)]
    public int StockQuantity { get; init; }
}
