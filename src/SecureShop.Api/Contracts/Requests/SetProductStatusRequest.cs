using System.ComponentModel.DataAnnotations;

namespace SecureShop.Api.Contracts.Requests;

public sealed class SetProductStatusRequest
{
    public bool IsActive { get; init; }

    [Required]
    public string RowVersion { get; init; } = string.Empty;
}
