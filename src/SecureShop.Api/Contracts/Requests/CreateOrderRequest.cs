using System.ComponentModel.DataAnnotations;

namespace SecureShop.Api.Contracts.Requests;

public sealed class CreateOrderRequest
{
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string RecipientName { get; init; } = string.Empty;

    [Required]
    [StringLength(500, MinimumLength = 5)]
    public string AddressLine { get; init; } = string.Empty;

    [Required]
    [StringLength(20, MinimumLength = 2)]
    public string PostalCode { get; init; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string City { get; init; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Country { get; init; } = string.Empty;
}
