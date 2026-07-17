using System.ComponentModel.DataAnnotations;
namespace SecureShop.Mvc.Models.ViewModels;
public sealed class LoginViewModel
{
    [Required, EmailAddress, Display(Name="E-posta")] public string Email { get; set; }=string.Empty;
    [Required, DataType(DataType.Password), Display(Name="Parola")] public string Password { get; set; }=string.Empty;

    [StringLength(2048)]
    public string? ReturnUrl { get; set; }
}
