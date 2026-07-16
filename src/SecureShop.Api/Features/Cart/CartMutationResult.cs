using SecureShop.Api.Contracts.Responses;

namespace SecureShop.Api.Features.Cart;

public enum CartMutationStatus
{
    Succeeded,
    ProductUnavailable,
    InsufficientStock,
    ItemNotFound,
    ConcurrencyConflict
}

public sealed record CartMutationResult(
    CartMutationStatus Status,
    CartResponse? Cart = null);
