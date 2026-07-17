using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureShop.Mvc.Models.ViewModels;
using SecureShop.Mvc.Services.Interfaces;

namespace SecureShop.Mvc.Controllers;

[AllowAnonymous]
[Route("products")]
public sealed class ProductsController : Controller
{
    private readonly IProductApiService _productApiService;

    public ProductsController(IProductApiService productApiService)
    {
        _productApiService = productApiService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var result = await _productApiService.GetProductsAsync(cancellationToken);
        return View(new ProductListViewModel
        {
            Products = result.Data ?? [],
            ErrorMessage = result.ErrorMessage
        });
    }

    [HttpGet("{sku}")]
    public async Task<IActionResult> Details(
        string sku,
        CancellationToken cancellationToken)
    {
        var result = await _productApiService.GetProductBySkuAsync(
            sku,
            cancellationToken);

        if (result.StatusCode == HttpStatusCode.NotFound)
        {
            return NotFound();
        }

        if (!result.IsSuccess || result.Data is null)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
            return RedirectToAction(nameof(Index));
        }

        return View(result.Data);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> DetailsById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _productApiService.GetProductAsync(
            id,
            cancellationToken);

        if (result.StatusCode == HttpStatusCode.NotFound)
        {
            return NotFound();
        }

        if (!result.IsSuccess || result.Data is null)
        {
            TempData["ErrorMessage"] = result.ErrorMessage;
            return RedirectToAction(nameof(Index));
        }

        return RedirectToAction(
            nameof(Details),
            new
            {
                sku = result.Data.Sku
            });
    }
}
