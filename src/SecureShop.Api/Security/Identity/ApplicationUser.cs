using Microsoft.AspNetCore.Identity;

namespace SecureShop.Api.Security.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    private ApplicationUser()
    {
        Id = Guid.NewGuid();
        IsActive = true;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public ApplicationUser(
        string email,
        string firstName,
        string lastName)
        : this()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var trimmedEmail = email.Trim();

        if (trimmedEmail.Length > 256)
        {
            throw new ArgumentOutOfRangeException(
                nameof(email),
                "E-posta adresi 256 karakterden uzun olamaz.");
        }

        Email = trimmedEmail;
        UserName = trimmedEmail;

        SetProfile(firstName, lastName);
    }

    [PersonalData]
    public string FirstName { get; private set; } = string.Empty;

    [PersonalData]
    public string LastName { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    [PersonalData]
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public ICollection<ApplicationUserRole> UserRoles { get; private set; } =
        new List<ApplicationUserRole>();

    public void SetProfile(
        string firstName,
        string lastName)
    {
        FirstName = NormalizeName(
            firstName,
            nameof(firstName),
            "Ad");

        LastName = NormalizeName(
            lastName,
            nameof(lastName),
            "Soyad");
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    private static string NormalizeName(
        string value,
        string parameterName,
        string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            value,
            parameterName);

        var normalizedValue = value.Trim();

        if (normalizedValue.Length > 100)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"{displayName} 100 karakterden uzun olamaz.");
        }

        return normalizedValue;
    }
}
