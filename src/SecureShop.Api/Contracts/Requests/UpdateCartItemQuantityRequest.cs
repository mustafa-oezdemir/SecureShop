using System.ComponentModel.DataAnnotations;

namespace SecureShop.Api.Contracts.Requests;

public sealed class UpdateCartItemQuantityRequest
{
    [Range(1, 99)]
    public int Quantity { get; init; }
}
