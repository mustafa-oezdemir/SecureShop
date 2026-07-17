namespace SecureShop.Mvc.Models.Requests;

public sealed record CreateOrderRequest(
    string RecipientName,
    string AddressLine,
    string PostalCode,
    string City,
    string Country);
