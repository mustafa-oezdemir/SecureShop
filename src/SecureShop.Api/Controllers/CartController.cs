using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureShop.Api.Contracts.Requests;
using SecureShop.Api.Contracts.Responses;
using SecureShop.Api.Features.Cart;
using SecureShop.Api.Security;
using SecureShop.Api.Security.Policies;

namespace SecureShop.Api.Controllers;

[ApiController]
[Route("api/cart")]
[Authorize(Policy = AppPolicies.CustomerOnly)]
public sealed class CartController : ControllerBase
{
    private readonly ICurrentUserService _currentUser;
    private readonly ICartService _cartService;

    public CartController(
        ICurrentUserService currentUser,
        ICartService cartService)
    {
        _currentUser = currentUser;
        _cartService = cartService;
    }

    [HttpGet]
    public async Task<ActionResult<CartResponse>> Get(
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not Guid userId)
        {
            return Unauthorized(CreateProblem(
                "Kimliği doğrulanmış kullanıcı bilgisi bulunamadı."));
        }

        return Ok(await _cartService.GetAsync(
            userId,
            cancellationToken));
    }

    [HttpPost("items")]
    public async Task<ActionResult<CartResponse>> AddItem(
        AddCartItemRequest request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not Guid userId)
        {
            return Unauthorized(CreateProblem(
                "Kimliği doğrulanmış kullanıcı bilgisi bulunamadı."));
        }

        var result = await _cartService.AddItemAsync(
            userId,
            request.ProductId,
            request.Quantity,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpPut("items/{itemId:guid}")]
    public async Task<ActionResult<CartResponse>> UpdateItem(
        Guid itemId,
        UpdateCartItemQuantityRequest request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not Guid userId)
        {
            return Unauthorized(CreateProblem(
                "Kimliği doğrulanmış kullanıcı bilgisi bulunamadı."));
        }

        var result = await _cartService.UpdateItemAsync(
            userId,
            itemId,
            request.Quantity,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpDelete("items/{itemId:guid}")]
    public async Task<ActionResult<CartResponse>> RemoveItem(
        Guid itemId,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not Guid userId)
        {
            return Unauthorized(CreateProblem(
                "Kimliği doğrulanmış kullanıcı bilgisi bulunamadı."));
        }

        var result = await _cartService.RemoveItemAsync(
            userId,
            itemId,
            cancellationToken);

        return ToActionResult(result);
    }

    [HttpDelete]
    public async Task<ActionResult<CartResponse>> Clear(
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not Guid userId)
        {
            return Unauthorized(CreateProblem(
                "Kimliği doğrulanmış kullanıcı bilgisi bulunamadı."));
        }

        var result = await _cartService.ClearAsync(
            userId,
            cancellationToken);

        return ToActionResult(result);
    }

    private ActionResult<CartResponse> ToActionResult(
        CartMutationResult result)
    {
        if (result.Status == CartMutationStatus.Succeeded
            && result.Cart is not null)
        {
            return Ok(result.Cart);
        }

        return result.Status switch
        {
            CartMutationStatus.ProductUnavailable =>
                BadRequest(CreateProblem(
                    "Ürün bulunamadı veya satışa açık değil.")),
            CartMutationStatus.InsufficientStock =>
                Conflict(CreateProblem(
                    "İstenen miktar kullanılabilir stok sınırını aşıyor.")),
            CartMutationStatus.ItemNotFound =>
                NotFound(CreateProblem("Sepet öğesi bulunamadı.")),
            CartMutationStatus.ConcurrencyConflict =>
                Conflict(CreateProblem(
                    "Sepet başka bir istek tarafından değiştirildi. Sayfayı yenileyin.")),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    private static ProblemDetails CreateProblem(string detail) =>
        new()
        {
            Detail = detail
        };
}
