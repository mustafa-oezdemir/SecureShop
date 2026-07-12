namespace SecureShop.Api.Domain.Constants;

public static class AppRoles
{
    public const string Admin = "Admin";

    public const string Employee = "Employee";

    public const string Kunde = "Kunde";

    public static IReadOnlyCollection<string> All { get; } =
    [
        Admin,
        Employee,
        Kunde
    ];
}
