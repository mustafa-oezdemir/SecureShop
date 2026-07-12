using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecureShop.Api.Security.Identity;

namespace SecureShop.Api.Data.Configurations;

public sealed class ApplicationRoleConfiguration
    : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(
        EntityTypeBuilder<ApplicationRole> builder)
    {
        builder.ToTable("AspNetRoles");

        builder.Property(role => role.Description)
            .HasMaxLength(256)
            .HasDefaultValue(string.Empty)
            .IsRequired();

        builder.Property(role => role.IsSystem)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(role => role.CreatedAtUtc)
            .HasPrecision(0)
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .ValueGeneratedOnAdd()
            .IsRequired();
    }
}
