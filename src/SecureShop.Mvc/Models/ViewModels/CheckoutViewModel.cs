using System.ComponentModel.DataAnnotations;
using SecureShop.Mvc.Models.Responses;

namespace SecureShop.Mvc.Models.ViewModels;

public sealed class CheckoutViewModel
{
    [Required]
    [StringLength(200, MinimumLength = 2)]
    [Display(Name = "Teslim alacak kişi")]
    public string RecipientName { get; set; } = string.Empty;

    [Required]
    [StringLength(500, MinimumLength = 5)]
    [Display(Name = "Adres")]
    public string AddressLine { get; set; } = string.Empty;

    [Required]
    [StringLength(20, MinimumLength = 2)]
    [Display(Name = "Posta kodu")]
    public string PostalCode { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 2)]
    [Display(Name = "Şehir")]
    public string City { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 2)]
    [Display(Name = "Ülke")]
    public string Country { get; set; } = "Almanya";

    public CartResponse? Cart { get; set; }
}
