using System.ComponentModel.DataAnnotations;

namespace SecureShop.Api.Contracts.Requests;

public sealed class VerifyOrderQrRequest
{
    [Required]
    [StringLength(4000, MinimumLength = 20)]
    public string Token { get; init; } = string.Empty;
}
