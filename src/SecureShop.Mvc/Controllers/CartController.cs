using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureShop.Mvc.Models.Requests;
using SecureShop.Mvc.Models.ViewModels;
using SecureShop.Mvc.Security;
using SecureShop.Mvc.Services.Interfaces;

namespace SecureShop.Mvc.Controllers;

[Authorize(Roles = AppRoles.Customer)]
[Route("cart")]
public sealed class CartController : Controller
{
    private readonly ICartApiService _cartApiService;

    public CartController(ICartApiService cartApiService)
    {
        _cartApiService = cartApiService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken)
    {
        var result = await _cartApiService.GetAsync(cancellationToken);

        return View(new CartViewModel
        {
            Cart = result.Data,
            ErrorMessage = result.ErrorMessage
        });
    }

    [HttpPost("items")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(
        AddCartItemRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] =
                "Geçerli bir ürün ve adet seçin.";

            return RedirectToAction(nameof(Index));
        }

        var result = await _cartApiService.AddItemAsync(
            request,
            cancellationToken);

        SetResultMessage(
            result.IsSuccess,
            result.ErrorMessage,
            "Ürün sepete eklendi.");

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("items/{itemId:guid}/quantity")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(
        Guid itemId,
        UpdateCartItemQuantityRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            if (WantsJsonResponse())
            {
                return BadRequest(new
                {
                    error = "Adet 1 ile 99 arasında olmalıdır."
                });
            }

            TempData["ErrorMessage"] =
                "Adet 1 ile 99 arasında olmalıdır.";

            return RedirectToAction(nameof(Index));
        }

        var result = await _cartApiService.UpdateItemAsync(
            itemId,
            request,
            cancellationToken);

        if (WantsJsonResponse())
        {
            if (result.IsSuccess && result.Data is not null)
            {
                return Ok(result.Data);
            }

            return StatusCode(
                (int)result.StatusCode,
                new
                {
                    error = result.ErrorMessage
                        ?? "Sepet miktarı güncellenemedi."
                });
        }

        SetResultMessage(
            result.IsSuccess,
            result.ErrorMessage,
            "Sepet miktarı güncellendi.");

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("items/{itemId:guid}/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(
        Guid itemId,
        CancellationToken cancellationToken)
    {
        var result = await _cartApiService.RemoveItemAsync(
            itemId,
            cancellationToken);

        SetResultMessage(
            result.IsSuccess,
            result.ErrorMessage,
            "Ürün sepetten çıkarıldı.");

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("clear")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Clear(
        CancellationToken cancellationToken)
    {
        var result = await _cartApiService.ClearAsync(
            cancellationToken);

        SetResultMessage(
            result.IsSuccess,
            result.ErrorMessage,
            "Sepet temizlendi.");

        return RedirectToAction(nameof(Index));
    }

    private void SetResultMessage(
        bool succeeded,
        string? errorMessage,
        string successMessage)
    {
        TempData[succeeded ? "SuccessMessage" : "ErrorMessage"] =
            succeeded
                ? successMessage
                : errorMessage ?? "Sepet işlemi tamamlanamadı.";
    }

    private bool WantsJsonResponse() =>
        Request.Headers.Accept.Any(value =>
            value?.Contains(
                "application/json",
                StringComparison.OrdinalIgnoreCase) == true)
        || string.Equals(
            Request.Headers.XRequestedWith,
            "XMLHttpRequest",
            StringComparison.OrdinalIgnoreCase);
}
