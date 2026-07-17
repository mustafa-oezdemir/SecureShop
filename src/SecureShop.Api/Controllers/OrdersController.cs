using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureShop.Api.Contracts.Requests;
using SecureShop.Api.Contracts.Responses;
using SecureShop.Api.Features.Orders;
using SecureShop.Api.Security;
using SecureShop.Api.Security.Policies;

namespace SecureShop.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize(Policy = AppPolicies.CustomerOnly)]
public sealed class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly ICurrentUserService _currentUser;

    public OrdersController(
        IOrderService orderService,
        ICurrentUserService currentUser)
    {
        _orderService = orderService;
        _currentUser = currentUser;
    }

    [HttpPost]
    public async Task<ActionResult<OrderResponse>> Create(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var result = await _orderService.CreateAsync(
            userId,
            request,
            cancellationToken);

        return ToActionResult(result, created: true);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrderResponse>>> GetMine(
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        return Ok(await _orderService.GetCustomerOrdersAsync(
            userId,
            cancellationToken));
    }

    [HttpGet("{orderNumber}")]
    public async Task<ActionResult<OrderResponse>> GetMineByNumber(
        string orderNumber,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not Guid userId)
        {
            return Unauthorized();
        }

        var order = await _orderService.GetCustomerOrderAsync(
            userId,
            orderNumber,
            cancellationToken);

        return order is null ? NotFound() : Ok(order);
    }

    private ActionResult<OrderResponse> ToActionResult(
        OrderMutationResult result,
        bool created)
    {
        if (result.Status == OrderMutationStatus.Succeeded
            && result.Order is not null)
        {
            return created
                ? CreatedAtAction(
                    nameof(GetMineByNumber),
                    new
                    {
                        orderNumber = result.Order.OrderNumber
                    },
                    result.Order)
                : Ok(result.Order);
        }

        return result.Status switch
        {
            OrderMutationStatus.CartEmpty =>
                BadRequest(Problem("Sepet boş.")),
            OrderMutationStatus.ProductUnavailable =>
                Conflict(Problem("Sepette satışa kapalı ürün var.")),
            OrderMutationStatus.InsufficientStock =>
                Conflict(Problem("Ürün stoğu sipariş için yetersiz.")),
            OrderMutationStatus.ConcurrencyConflict =>
                Conflict(Problem("Sepet veya stok değişti. Sayfayı yenileyin.")),
            _ => StatusCode(
                StatusCodes.Status500InternalServerError)
        };
    }

    private static ProblemDetails Problem(string detail) =>
        new() { Detail = detail };
}
