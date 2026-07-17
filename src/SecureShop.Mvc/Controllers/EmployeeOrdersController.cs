using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureShop.Mvc.Http;
using SecureShop.Mvc.Models.Requests;
using SecureShop.Mvc.Models.Responses;
using SecureShop.Mvc.Models.ViewModels;
using SecureShop.Mvc.Security;
using SecureShop.Mvc.Services.Interfaces;

namespace SecureShop.Mvc.Controllers;

[Authorize(Roles = AppRoles.Admin + "," + AppRoles.Employee)]
[Route("employee/orders")]
public sealed class EmployeeOrdersController : Controller
{
    private readonly IOrderApiService _orderApiService;

    public EmployeeOrdersController(
        IOrderApiService orderApiService)
    {
        _orderApiService = orderApiService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken)
    {
        var result = await _orderApiService.GetStaffAsync(
            cancellationToken);

        return View(new OrderListViewModel
        {
            Orders = result.Data ?? [],
            ErrorMessage = result.ErrorMessage
        });
    }

    [HttpGet("{orderNumber}")]
    public async Task<IActionResult> Details(
        string orderNumber,
        CancellationToken cancellationToken)
    {
        var result = await _orderApiService.GetStaffAsync(
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

    [HttpPost("{orderNumber}/approve")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Approve(
        string orderNumber,
        string rowVersion,
        CancellationToken cancellationToken) =>
        ProcessAsync(
            orderNumber,
            () => _orderApiService.ApproveAsync(
                orderNumber,
                new ProcessOrderRequest(rowVersion),
                cancellationToken));

    [HttpPost("{orderNumber}/ready")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> MarkReady(
        string orderNumber,
        string rowVersion,
        CancellationToken cancellationToken) =>
        ProcessAsync(
            orderNumber,
            () => _orderApiService.MarkReadyAsync(
                orderNumber,
                new ProcessOrderRequest(rowVersion),
                cancellationToken));

    [HttpPost("{orderNumber}/cancel")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Cancel(
        string orderNumber,
        string rowVersion,
        CancellationToken cancellationToken) =>
        ProcessAsync(
            orderNumber,
            () => _orderApiService.CancelAsync(
                orderNumber,
                new ProcessOrderRequest(rowVersion),
                cancellationToken));

    [HttpGet("verify")]
    public IActionResult Verify([FromQuery] string? token) =>
        View(new QrVerificationViewModel
        {
            Token = token ?? string.Empty
        });

    [HttpPost("verify")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Verify(
        QrVerificationViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _orderApiService.VerifyQrAsync(
            new VerifyOrderQrRequest(model.Token),
            cancellationToken);

        if (!result.IsSuccess || result.Data is null)
        {
            ModelState.AddModelError(
                string.Empty,
                result.ErrorMessage ?? "QR doğrulanamadı.");

            return View(model);
        }

        model.Order = result.Data;
        TempData["SuccessMessage"] =
            "QR doğrulandı; sipariş teslim edildi olarak tamamlandı.";

        return View(model);
    }

    private async Task<IActionResult> ProcessAsync(
        string orderNumber,
        Func<Task<ApiResponse<OrderResponse>>> operation)
    {
        var result = await operation();

        TempData[result.IsSuccess
            ? "SuccessMessage"
            : "ErrorMessage"] = result.IsSuccess
                ? "Sipariş durumu güncellendi."
                : result.ErrorMessage
                    ?? "Sipariş durumu güncellenemedi.";

        return RedirectToAction(
            nameof(Details),
            new
            {
                orderNumber
            });
    }
}
