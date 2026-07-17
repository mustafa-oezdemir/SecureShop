using System.ComponentModel.DataAnnotations;

namespace SecureShop.Api.Contracts.Requests;

public sealed class ProcessOrderRequest
{
    [Required]
    public string RowVersion { get; init; } = string.Empty;
}
