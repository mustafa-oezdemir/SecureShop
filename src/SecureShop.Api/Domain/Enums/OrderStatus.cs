namespace SecureShop.Api.Domain.Enums;

public enum OrderStatus
{
    PendingApproval = 1,
    Approved = 2,
    ReadyForPickup = 3,
    Completed = 4,
    Cancelled = 5
}
