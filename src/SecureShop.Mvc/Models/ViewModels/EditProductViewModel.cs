using System.ComponentModel.DataAnnotations;
using SecureShop.Mvc.Models.Responses;

namespace SecureShop.Mvc.Models.ViewModels;

public sealed class EditProductViewModel
{
    public Guid Id { get; set; }

    [Required(ErrorMessage = "Kategori seçin.")]
    [Display(Name = "Kategori")]
    public Guid CategoryId { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 2)]
    [Display(Name = "Ürün adı")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(64, MinimumLength = 1)]
    [RegularExpression(
        "^[A-Za-z0-9._-]+$",
        ErrorMessage = "SKU yalnızca harf, rakam, nokta, alt çizgi ve tire içerebilir.")]
    public string Sku { get; set; } = string.Empty;

    [StringLength(2000)]
    [Display(Name = "Açıklama")]
    public string? Description { get; set; }

    [Range(
        typeof(decimal),
        "0",
        "9999999999999999.99",
        ParseLimitsInInvariantCulture = true)]
    [Display(Name = "Fiyat")]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue)]
    [Display(Name = "Stok adedi")]
    public int StockQuantity { get; set; }

    [Required]
    public string RowVersion { get; set; } = string.Empty;

    public IReadOnlyList<ProductImageResponse> Images { get; set; } = [];

    public IReadOnlyList<CategoryOptionResponse> Categories { get; set; } = [];
}
