using System.ComponentModel.DataAnnotations;
namespace SecureShop.Api.Contracts.Requests;
public sealed class LoginRequest
{
    [Required, EmailAddress, StringLength(256)] public string Email { get; init; } = string.Empty;
    [Required, StringLength(200, MinimumLength = 1)] public string Password { get; init; } = string.Empty;
}
