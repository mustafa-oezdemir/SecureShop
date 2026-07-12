using System.ComponentModel.DataAnnotations;

namespace SecureShop.Api.Contracts.Requests;

public sealed class EnableAuthenticatorRequest
{
    [Required(ErrorMessage = "Dogrulama kodu zorunludur.")]
    [StringLength(
        32,
        MinimumLength = 6,
        ErrorMessage = "Dogrulama kodu gecerli degildir.")]
    public string Code { get; init; } = string.Empty;
}
