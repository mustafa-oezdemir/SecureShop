using SecureShop.Mvc.Models.Responses;

namespace SecureShop.Mvc.Models.ViewModels;

public sealed class OrderListViewModel
{
    public IReadOnlyList<OrderResponse> Orders { get; init; } = [];

    public string? ErrorMessage { get; init; }
}
