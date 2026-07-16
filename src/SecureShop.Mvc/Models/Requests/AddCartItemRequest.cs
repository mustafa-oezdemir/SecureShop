using System.ComponentModel.DataAnnotations;

namespace SecureShop.Mvc.Models.Requests;

public sealed class AddCartItemRequest
{
    [Required]
    public Guid ProductId { get; init; }

    [Range(1, 99, ErrorMessage = "Adet 1 ile 99 arasında olmalıdır.")]
    public int Quantity { get; init; } = 1;
}
