using Microsoft.AspNetCore.Identity;

namespace SecureShop.Api.Security.Identity;

public sealed class ApplicationUserRole : IdentityUserRole<Guid>
{
    public ApplicationUser User { get; private set; } = null!;

    public ApplicationRole Role { get; private set; } = null!;
}
