using SecureShop.Api.Contracts.Responses;

namespace SecureShop.Api.Features.Products;

public enum ProductMutationStatus
{
    Succeeded,
    NotFound,
    CategoryNotFound,
    DuplicateSku,
    InvalidRowVersion,
    ConcurrencyConflict
}

public sealed record ProductMutationResult(
    ProductMutationStatus Status,
    ProductResponse? Product = null);
