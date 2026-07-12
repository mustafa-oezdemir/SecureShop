namespace SecureShop.Api.Security.Policies;

public static class AppPolicies
{
    public const string AdminOnly = nameof(AdminOnly);

    public const string StaffOnly = nameof(StaffOnly);

    public const string CustomerOnly = nameof(CustomerOnly);
}
