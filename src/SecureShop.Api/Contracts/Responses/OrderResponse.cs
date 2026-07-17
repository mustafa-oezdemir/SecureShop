namespace SecureShop.Api.Contracts.Responses;

public sealed record OrderResponse(
    Guid Id,
    string OrderNumber,
    Guid UserId,
    string RecipientName,
    string AddressLine,
    string PostalCode,
    string City,
    string Country,
    string Status,
    decimal TotalAmount,
    IReadOnlyList<OrderItemResponse> Items,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string RowVersion,
    string? QrCodeDataUrl);
