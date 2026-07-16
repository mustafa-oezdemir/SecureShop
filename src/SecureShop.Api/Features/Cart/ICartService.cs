using SecureShop.Api.Contracts.Responses;

namespace SecureShop.Api.Features.Cart;

public interface ICartService
{
    Task<CartResponse> GetAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<CartMutationResult> AddItemAsync(
        Guid userId,
        Guid productId,
        int quantity,
        CancellationToken cancellationToken);

    Task<CartMutationResult> UpdateItemAsync(
        Guid userId,
        Guid itemId,
        int quantity,
        CancellationToken cancellationToken);

    Task<CartMutationResult> RemoveItemAsync(
        Guid userId,
        Guid itemId,
        CancellationToken cancellationToken);

    Task<CartMutationResult> ClearAsync(
        Guid userId,
        CancellationToken cancellationToken);
}
