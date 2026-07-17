using System.ComponentModel.DataAnnotations;
using SecureShop.Mvc.Models.Responses;

namespace SecureShop.Mvc.Models.ViewModels;

public sealed class QrVerificationViewModel
{
    [Required]
    [Display(Name = "QR doğrulama token'ı")]
    public string Token { get; set; } = string.Empty;

    public OrderResponse? Order { get; set; }
}
