using SecureShop.Mvc.Models.Responses;

namespace SecureShop.Mvc.Models.ViewModels;

public sealed class AuthSessionViewModel
{
    public bool MvcIsAuthenticated { get; init; }

    public string? MvcUserName { get; init; }

    public int ApiStatusCode { get; init; }

    public AuthSessionResponse? ApiSession { get; init; }

    public string? ErrorMessage { get; init; }
}
