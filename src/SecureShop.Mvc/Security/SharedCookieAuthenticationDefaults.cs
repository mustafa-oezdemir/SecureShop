namespace SecureShop.Mvc.Security;

public static class SharedCookieAuthenticationDefaults
{
    public const string AuthenticationScheme =
        "Identity.Application";

    public const string CookieName =
        "__Host-SecureShop.Auth";

    public const string DataProtectionApplicationName =
        "SecureShop.SharedCookie.v1";

    public const string KeyRingPathConfigurationKey =
        "Authentication:SharedCookie:KeyRingPath";
}
