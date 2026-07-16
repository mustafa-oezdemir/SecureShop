using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using SecureShop.Mvc.Models.Responses;

namespace SecureShop.Mvc.Models.ViewModels;

public sealed class CreateProductViewModel
{
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

    [Range(typeof(decimal), "0", "9999999999999999.99")]
    [Display(Name = "Fiyat")]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue)]
    [Display(Name = "Stok adedi")]
    public int StockQuantity { get; set; }

    [Required(ErrorMessage = "En az bir ürün fotoğrafı seçin.")]
    [Display(Name = "Ürün fotoğrafları")]
    public List<IFormFile> Images { get; set; } = [];

    public IReadOnlyList<CategoryOptionResponse> Categories { get; set; } = [];
}
