namespace SecureShop.Mvc.Models.Requests;

public sealed record SetProductStatusRequest(
    bool IsActive,
    string RowVersion);
