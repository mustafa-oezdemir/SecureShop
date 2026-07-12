using Microsoft.AspNetCore.Identity;

namespace SecureShop.Api.Security.Identity;

public sealed class ApplicationRole : IdentityRole<Guid>
{
    private ApplicationRole()
    {
        Id = Guid.NewGuid();
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public ApplicationRole(
        string name,
        string description,
        bool isSystem)
        : this()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var normalizedName = name.Trim();

        if (normalizedName.Length > 256)
        {
            throw new ArgumentOutOfRangeException(
                nameof(name),
                "Rol adı 256 karakterden uzun olamaz.");
        }

        Name = normalizedName;

        SetMetadata(description, isSystem);
    }

    public string Description { get; private set; } = string.Empty;

    public bool IsSystem { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public ICollection<ApplicationUserRole> UserRoles { get; private set; } =
        new List<ApplicationUserRole>();

    public bool SetMetadata(
        string description,
        bool isSystem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        var normalizedDescription = description.Trim();

        if (normalizedDescription.Length > 256)
        {
            throw new ArgumentOutOfRangeException(
                nameof(description),
                "Rol açıklaması 256 karakterden uzun olamaz.");
        }

        if (Description == normalizedDescription
            && IsSystem == isSystem)
        {
            return false;
        }

        Description = normalizedDescription;
        IsSystem = isSystem;

        return true;
    }
}
