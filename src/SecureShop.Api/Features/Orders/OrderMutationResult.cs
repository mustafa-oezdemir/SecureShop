using SecureShop.Api.Contracts.Responses;

namespace SecureShop.Api.Features.Orders;

public enum OrderMutationStatus
{
    Succeeded,
    CartEmpty,
    ProductUnavailable,
    InsufficientStock,
    NotFound,
    Forbidden,
    InvalidTransition,
    InvalidRowVersion,
    InvalidQrCode,
    ConcurrencyConflict
}

public sealed record OrderMutationResult(
    OrderMutationStatus Status,
    OrderResponse? Order = null);
