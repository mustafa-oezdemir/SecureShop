using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureShop.Mvc.Models.Requests;
using SecureShop.Mvc.Models.ViewModels;
using SecureShop.Mvc.Security;
using SecureShop.Mvc.Services.Interfaces;

namespace SecureShop.Mvc.Controllers;

[Authorize(Roles = AppRoles.Admin)]
[Route("admin/products")]
public sealed class AdminProductsController : Controller
{
    private const int MaximumImageCount = 10;

    private readonly IProductApiService _productApiService;
    private readonly IProductImageStorage _imageStorage;

    public AdminProductsController(
        IProductApiService productApiService,
        IProductImageStorage imageStorage)
    {
        _productApiService = productApiService;
        _imageStorage = imageStorage;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken)
    {
        var result = await _productApiService
            .GetManagementProductsAsync(cancellationToken);

        return View(new AdminProductListViewModel
        {
            Products = result.Data ?? [],
            ErrorMessage = result.ErrorMessage
        });
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create(
        CancellationToken cancellationToken)
    {
        var model = new CreateProductViewModel();

        await LoadCategoriesAsync(model, cancellationToken);

        return View(model);
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(55 * 1024 * 1024)]
    public async Task<IActionResult> Create(
        CreateProductViewModel model,
        CancellationToken cancellationToken)
    {
        if (model.Images.Count is < 1 or > MaximumImageCount)
        {
            ModelState.AddModelError(
                nameof(model.Images),
                $"1 ile {MaximumImageCount} arasında fotoğraf seçin.");
        }

        if (!ModelState.IsValid)
        {
            await LoadCategoriesAsync(model, cancellationToken);
            return View(model);
        }

        var storageResult = await _imageStorage.SaveAsync(
            model.Images,
            model.Sku.Trim().ToUpperInvariant(),
            model.Name,
            cancellationToken);

        if (!storageResult.Succeeded)
        {
            ModelState.AddModelError(
                nameof(model.Images),
                storageResult.ErrorMessage
                    ?? "Ürün fotoğrafları kaydedilemedi.");

            await LoadCategoriesAsync(model, cancellationToken);
            return View(model);
        }

        var request = new CreateProductRequest(
            model.CategoryId,
            model.Name.Trim(),
            model.Sku.Trim(),
            string.IsNullOrWhiteSpace(model.Description)
                ? null
                : model.Description.Trim(),
            model.Price,
            model.StockQuantity,
            storageResult.Images);

        var result = await _productApiService.CreateProductAsync(
            request,
            cancellationToken);

        if (!result.IsSuccess || result.Data is null)
        {
            await _imageStorage.DeleteAsync(
                storageResult.FolderName,
                cancellationToken);

            ModelState.AddModelError(
                string.Empty,
                result.ErrorMessage ?? "Ürün oluşturulamadı.");

            await LoadCategoriesAsync(model, cancellationToken);
            return View(model);
        }

        TempData["SuccessMessage"] =
            "Ürün ve fotoğrafları başarıyla oluşturuldu.";

        return RedirectToAction(
            "Details",
            "Products",
            new
            {
                sku = result.Data.Sku
            });
    }

    [HttpGet("{sku}/edit")]
    public async Task<IActionResult> Edit(
        string sku,
        CancellationToken cancellationToken)
    {
        var result = await _productApiService
            .GetManagementProductBySkuAsync(sku, cancellationToken);

        if (!result.IsSuccess || result.Data is null)
        {
            TempData["ErrorMessage"] =
                result.ErrorMessage ?? "Ürün bulunamadı.";

            return RedirectToAction(nameof(Index));
        }

        var product = result.Data;
        var model = new EditProductViewModel
        {
            Id = product.Id,
            CategoryId = product.CategoryId,
            Name = product.Name,
            Sku = product.Sku,
            Description = product.Description,
            Price = product.Price,
            StockQuantity = product.StockQuantity,
            RowVersion = product.RowVersion,
            Images = product.Images
        };

        await LoadCategoriesAsync(model, cancellationToken);

        return View(model);
    }

    [HttpGet("{id:guid}/edit")]
    public async Task<IActionResult> EditById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _productApiService
            .GetManagementProductAsync(id, cancellationToken);

        if (!result.IsSuccess || result.Data is null)
        {
            TempData["ErrorMessage"] =
                result.ErrorMessage ?? "Ürün bulunamadı.";

            return RedirectToAction(nameof(Index));
        }

        return RedirectToAction(
            nameof(Edit),
            new
            {
                sku = result.Data.Sku
            });
    }

    [HttpPost("{sku}/edit")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(55 * 1024 * 1024)]
    public async Task<IActionResult> Edit(
        string sku,
        EditProductViewModel model,
        CancellationToken cancellationToken)
    {
        var currentResult = await _productApiService
            .GetManagementProductBySkuAsync(sku, cancellationToken);

        if (!currentResult.IsSuccess || currentResult.Data is null)
        {
            TempData["ErrorMessage"] =
                currentResult.ErrorMessage ?? "Ürün bulunamadı.";

            return RedirectToAction(nameof(Index));
        }

        var currentProduct = currentResult.Data;

        if (currentProduct.Id != model.Id)
        {
            return BadRequest();
        }

        model.Images = currentProduct.Images;

        if (currentProduct.Images.Count + model.NewImages.Count
            > MaximumImageCount)
        {
            ModelState.AddModelError(
                nameof(model.NewImages),
                $"Bir üründe en fazla {MaximumImageCount} fotoğraf olabilir.");
        }

        if (!ModelState.IsValid)
        {
            await LoadCategoriesAsync(model, cancellationToken);
            return View(model);
        }

        IReadOnlyList<CreateProductImageRequest> newImages = [];

        if (model.NewImages.Count > 0)
        {
            var storageResult = await _imageStorage.SaveAdditionalAsync(
                model.NewImages,
                model.Sku.Trim().ToUpperInvariant(),
                model.Name,
                currentProduct.Images.Count,
                cancellationToken);

            if (!storageResult.Succeeded)
            {
                ModelState.AddModelError(
                    nameof(model.NewImages),
                    storageResult.ErrorMessage
                        ?? "Yeni ürün fotoğrafları kaydedilemedi.");

                await LoadCategoriesAsync(model, cancellationToken);
                return View(model);
            }

            newImages = storageResult.Images;
        }

        var request = new UpdateProductRequest(
            model.CategoryId,
            model.Name.Trim(),
            model.Sku.Trim(),
            string.IsNullOrWhiteSpace(model.Description)
                ? null
                : model.Description.Trim(),
            model.Price,
            model.StockQuantity,
            model.RowVersion,
            newImages);

        var result = await _productApiService.UpdateProductAsync(
            model.Id,
            request,
            cancellationToken);

        if (!result.IsSuccess || result.Data is null)
        {
            if (newImages.Count > 0)
            {
                await _imageStorage.DeleteFilesAsync(
                    newImages,
                    cancellationToken);
            }

            ModelState.AddModelError(
                string.Empty,
                result.ErrorMessage ?? "Ürün güncellenemedi.");

            await LoadCategoriesAsync(model, cancellationToken);
            return View(model);
        }

        TempData["SuccessMessage"] = newImages.Count > 0
            ? "Ürün ve yeni fotoğrafları başarıyla güncellendi."
            : "Ürün başarıyla güncellendi.";

        return RedirectToAction(
            nameof(Edit),
            new
            {
                sku = result.Data.Sku
            });
    }

    [HttpPost("{id:guid}/status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(
        Guid id,
        bool isActive,
        string rowVersion,
        CancellationToken cancellationToken)
    {
        var result = await _productApiService.SetProductStatusAsync(
            id,
            new SetProductStatusRequest(isActive, rowVersion),
            cancellationToken);

        TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] =
            result.IsSuccess
                ? isActive
                    ? "Ürün yeniden aktifleştirildi."
                    : "Ürün pasife alındı."
                : result.ErrorMessage ?? "Ürün durumu güncellenemedi.";

        return RedirectToAction(nameof(Index));
    }

    private async Task LoadCategoriesAsync(
        CreateProductViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await _productApiService
            .GetCategoryOptionsAsync(cancellationToken);

        model.Categories = result.Data ?? [];

        if (!result.IsSuccess)
        {
            ModelState.AddModelError(
                string.Empty,
                result.ErrorMessage
                    ?? "Kategori seçenekleri yüklenemedi.");
        }
    }

    private async Task LoadCategoriesAsync(
        EditProductViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await _productApiService
            .GetCategoryOptionsAsync(cancellationToken);

        model.Categories = result.Data ?? [];

        if (!result.IsSuccess)
        {
            ModelState.AddModelError(
                string.Empty,
                result.ErrorMessage
                    ?? "Kategori seçenekleri yüklenemedi.");
        }
    }
}
