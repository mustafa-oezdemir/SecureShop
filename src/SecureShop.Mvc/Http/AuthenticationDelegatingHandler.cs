using SecureShop.Mvc.Security;

namespace SecureShop.Mvc.Http;

public sealed class AuthenticationDelegatingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthenticationDelegatingHandler(
        IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        request.Headers.Remove("Cookie");

        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext is not null
            && httpContext.Request.Cookies.TryGetValue(
                SharedCookieAuthenticationDefaults.CookieName,
                out var authenticationCookie)
            && !string.IsNullOrWhiteSpace(authenticationCookie))
        {
            request.Headers.TryAddWithoutValidation(
                "Cookie",
                $"{SharedCookieAuthenticationDefaults.CookieName}={authenticationCookie}");
        }

        return base.SendAsync(request, cancellationToken);
    }
}
