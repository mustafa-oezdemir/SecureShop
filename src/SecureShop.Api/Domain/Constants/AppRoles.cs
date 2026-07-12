namespace SecureShop.Api.Domain.Constants;

public static class AppRoles
{
    public const string Admin = "Admin";

    public const string Employee = "Employee";

    public const string Kunde = "Kunde";

    public static IReadOnlyList<string> All { get; } =
    [
        Admin,
        Employee,
        Kunde
    ];
}
