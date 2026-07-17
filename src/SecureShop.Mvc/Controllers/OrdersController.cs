using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureShop.Mvc.Models.Requests;
using SecureShop.Mvc.Models.ViewModels;
using SecureShop.Mvc.Security;
using SecureShop.Mvc.Services.Interfaces;

namespace SecureShop.Mvc.Controllers;

[Authorize(Roles = AppRoles.Customer)]
[Route("orders")]
public sealed class OrdersController : Controller
{
    private readonly IOrderApiService _orderApiService;
    private readonly ICartApiService _cartApiService;

    public OrdersController(
        IOrderApiService orderApiService,
        ICartApiService cartApiService)
    {
        _orderApiService = orderApiService;
        _cartApiService = cartApiService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken)
    {
        var result = await _orderApiService.GetMineAsync(
            cancellationToken);

        return View(new OrderListViewModel
        {
            Orders = result.Data ?? [],
            ErrorMessage = result.ErrorMessage
        });
    }

    [HttpGet("checkout")]
    public async Task<IActionResult> Checkout(
        CancellationToken cancellationToken)
    {
        var cartResult = await _cartApiService.GetAsync(
            cancellationToken);

        if (!cartResult.IsSuccess
            || cartResult.Data is null
            || cartResult.Data.Items.Count == 0)
        {
            TempData["ErrorMessage"] =
                cartResult.ErrorMessage ?? "Sepetiniz boş.";

            return RedirectToAction("Index", "Cart");
        }

        return View(new CheckoutViewModel
        {
            Cart = cartResult.Data
        });
    }

    [HttpPost("checkout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(
        CheckoutViewModel model,
        CancellationToken cancellationToken)
    {
        var cartResult = await _cartApiService.GetAsync(
            cancellationToken);
        model.Cart = cartResult.Data;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _orderApiService.CreateAsync(
            new CreateOrderRequest(
                model.RecipientName.Trim(),
                model.AddressLine.Trim(),
                model.PostalCode.Trim(),
                model.City.Trim(),
                model.Country.Trim()),
            cancellationToken);

        if (!result.IsSuccess || result.Data is null)
        {
            ModelState.AddModelError(
                string.Empty,
                result.ErrorMessage
                    ?? "Sipariş oluşturulamadı.");

            return View(model);
        }

        TempData["SuccessMessage"] =
            "Siparişiniz oluşturuldu ve personel onayına gönderildi.";

        return RedirectToAction(
            nameof(Details),
            new
            {
                orderNumber = result.Data.OrderNumber
            });
    }

    [HttpGet("{orderNumber}")]
    public async Task<IActionResult> Details(
        string orderNumber,
        CancellationToken cancellationToken)
    {
        var result = await _orderApiService.GetMineAsync(
            orderNumber,
            cancellationToken);

        if (!result.IsSuccess || result.Data is null)
        {
            TempData["ErrorMessage"] =
                result.ErrorMessage ?? "Sipariş bulunamadı.";

            return RedirectToAction(nameof(Index));
        }

        return View(result.Data);
    }
}
