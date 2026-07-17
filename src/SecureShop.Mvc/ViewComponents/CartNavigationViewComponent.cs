using Microsoft.AspNetCore.Mvc;
using SecureShop.Mvc.Models.ViewModels;
using SecureShop.Mvc.Services.Interfaces;

namespace SecureShop.Mvc.ViewComponents;

public sealed class CartNavigationViewComponent
    : ViewComponent
{
    private readonly ICartApiService _cartApiService;

    public CartNavigationViewComponent(
        ICartApiService cartApiService)
    {
        _cartApiService = cartApiService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var result = await _cartApiService.GetAsync(
            HttpContext.RequestAborted);

        return View(new CartNavigationViewModel(
            result.Data?.TotalQuantity ?? 0));
    }
}
