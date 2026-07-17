using SecureShop.Mvc.Http;
using SecureShop.Mvc.Models.Requests;
using SecureShop.Mvc.Models.Responses;

namespace SecureShop.Mvc.Services.Interfaces;

public interface IOrderApiService
{
    Task<ApiResponse<OrderResponse>> CreateAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<IReadOnlyList<OrderResponse>>> GetMineAsync(
        CancellationToken cancellationToken = default);

    Task<ApiResponse<OrderResponse>> GetMineAsync(
        string orderNumber,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<IReadOnlyList<OrderResponse>>> GetStaffAsync(
        CancellationToken cancellationToken = default);

    Task<ApiResponse<OrderResponse>> GetStaffAsync(
        string orderNumber,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<OrderResponse>> ApproveAsync(
        string orderNumber,
        ProcessOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<OrderResponse>> MarkReadyAsync(
        string orderNumber,
        ProcessOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<OrderResponse>> CancelAsync(
        string orderNumber,
        ProcessOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<OrderResponse>> VerifyQrAsync(
        VerifyOrderQrRequest request,
        CancellationToken cancellationToken = default);
}
