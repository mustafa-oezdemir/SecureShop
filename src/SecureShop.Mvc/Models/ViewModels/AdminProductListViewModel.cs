using SecureShop.Mvc.Models.Responses;

namespace SecureShop.Mvc.Models.ViewModels;

public sealed class AdminProductListViewModel
{
    public IReadOnlyList<ProductResponse> Products { get; init; } = [];

    public string? ErrorMessage { get; init; }
}
