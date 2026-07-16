using SecureShop.Mvc.Http;
using SecureShop.Mvc.Models.Requests;
using SecureShop.Mvc.Models.Responses;

namespace SecureShop.Mvc.Services.Interfaces;

public interface ICartApiService
{
    Task<ApiResponse<CartResponse>> GetAsync(
        CancellationToken cancellationToken = default);

    Task<ApiResponse<CartResponse>> AddItemAsync(
        AddCartItemRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<CartResponse>> UpdateItemAsync(
        Guid itemId,
        UpdateCartItemQuantityRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<CartResponse>> RemoveItemAsync(
        Guid itemId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<CartResponse>> ClearAsync(
        CancellationToken cancellationToken = default);
}
