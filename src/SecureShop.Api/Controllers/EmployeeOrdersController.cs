using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureShop.Api.Contracts.Requests;
using SecureShop.Api.Contracts.Responses;
using SecureShop.Api.Features.Orders;
using SecureShop.Api.Security;
using SecureShop.Api.Security.Policies;

namespace SecureShop.Api.Controllers;

[ApiController]
[Route("api/employee/orders")]
[Authorize(Policy = AppPolicies.StaffOnly)]
public sealed class EmployeeOrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly ICurrentUserService _currentUser;

    public EmployeeOrdersController(
        IOrderService orderService,
        ICurrentUserService currentUser)
    {
        _orderService = orderService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrderResponse>>> GetAll(
        CancellationToken cancellationToken) =>
        Ok(await _orderService.GetStaffOrdersAsync(
            cancellationToken));

    [HttpGet("{orderNumber}")]
    public async Task<ActionResult<OrderResponse>> GetByNumber(
        string orderNumber,
        CancellationToken cancellationToken)
    {
        var order = await _orderService.GetStaffOrderAsync(
            orderNumber,
            cancellationToken);

        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost("{orderNumber}/approve")]
    public Task<ActionResult<OrderResponse>> Approve(
        string orderNumber,
        ProcessOrderRequest request,
        CancellationToken cancellationToken) =>
        ProcessAsync(
            (staffUserId, token) => _orderService.ApproveAsync(
                orderNumber,
                staffUserId,
                request.RowVersion,
                token),
            cancellationToken);

    [HttpPost("{orderNumber}/ready")]
    public Task<ActionResult<OrderResponse>> MarkReady(
        string orderNumber,
        ProcessOrderRequest request,
        CancellationToken cancellationToken) =>
        ProcessAsync(
            (staffUserId, token) => _orderService.MarkReadyAsync(
                orderNumber,
                staffUserId,
                request.RowVersion,
                token),
            cancellationToken);

    [HttpPost("{orderNumber}/cancel")]
    public Task<ActionResult<OrderResponse>> Cancel(
        string orderNumber,
        ProcessOrderRequest request,
        CancellationToken cancellationToken) =>
        ProcessAsync(
            (staffUserId, token) => _orderService.CancelAsync(
                orderNumber,
                staffUserId,
                request.RowVersion,
                token),
            cancellationToken);

    [HttpPost("verify-qr")]
    public Task<ActionResult<OrderResponse>> VerifyQr(
        VerifyOrderQrRequest request,
        CancellationToken cancellationToken) =>
        ProcessAsync(
            (staffUserId, token) => _orderService.CompleteByQrAsync(
                request.Token,
                staffUserId,
                token),
            cancellationToken);

    private async Task<ActionResult<OrderResponse>> ProcessAsync(
        Func<Guid, CancellationToken, Task<OrderMutationResult>> operation,
        CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not Guid staffUserId)
        {
            return Unauthorized();
        }

        var result = await operation(
            staffUserId,
            cancellationToken);

        if (result.Status == OrderMutationStatus.Succeeded
            && result.Order is not null)
        {
            return Ok(result.Order);
        }

        return result.Status switch
        {
            OrderMutationStatus.NotFound =>
                NotFound(Problem("Sipariş bulunamadı.")),
            OrderMutationStatus.InvalidRowVersion =>
                BadRequest(Problem("RowVersion geçersiz.")),
            OrderMutationStatus.InvalidQrCode =>
                BadRequest(Problem("QR kod geçersiz veya süresi dolmuş.")),
            OrderMutationStatus.InvalidTransition =>
                Conflict(Problem("Sipariş bu işleme uygun durumda değil.")),
            OrderMutationStatus.ConcurrencyConflict =>
                Conflict(Problem("Sipariş başka bir kullanıcı tarafından değiştirildi.")),
            _ => StatusCode(
                StatusCodes.Status500InternalServerError)
        };
    }

    private static ProblemDetails Problem(string detail) =>
        new() { Detail = detail };
}
