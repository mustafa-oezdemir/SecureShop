namespace SecureShop.Api.Domain.Entities;

public sealed class Category
{
    private Category()
    {
    }

    public Category(string name)
    {
        Id = Guid.NewGuid();
        SetName(name);
        IsActive = true;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public ICollection<Product> Products { get; private set; } = new List<Product>();

    public void SetName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var normalizedName = name.Trim();

        if (normalizedName.Length > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(name),
                "Kategori adı 100 karakterden uzun olamaz.");
        }

        Name = normalizedName;
        MarkAsUpdated();
    }

    public void Activate()
    {
        IsActive = true;
        MarkAsUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        MarkAsUpdated();
    }

    private void MarkAsUpdated()
    {
        if (CreatedAtUtc != default)
        {
            UpdatedAtUtc = DateTimeOffset.UtcNow;
        }
    }
}