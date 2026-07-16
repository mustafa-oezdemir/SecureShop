using SecureShop.Mvc.Models.Responses;

namespace SecureShop.Mvc.Models.ViewModels;

public sealed class CartViewModel
{
    public CartResponse? Cart { get; init; }

    public string? ErrorMessage { get; init; }
}
