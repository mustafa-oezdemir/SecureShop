using System.ComponentModel.DataAnnotations;

namespace SecureShop.Api.Contracts.Requests;

public sealed class AddCartItemRequest
{
    [Required]
    public Guid ProductId { get; init; }

    [Range(1, 99)]
    public int Quantity { get; init; } = 1;
}
