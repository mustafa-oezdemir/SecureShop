using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecureShop.Api.Domain.Entities;
using SecureShop.Api.Security.Identity;

namespace SecureShop.Api.Data.Configurations;

public sealed class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.ToTable("Carts");

        builder.HasKey(cart => cart.Id);

        builder.Property(cart => cart.Id)
            .ValueGeneratedNever();

        builder.Property(cart => cart.UserId)
            .IsRequired();

        builder.Property(cart => cart.CreatedAtUtc)
            .HasPrecision(0)
            .IsRequired();

        builder.Property(cart => cart.UpdatedAtUtc)
            .HasPrecision(0)
            .IsRequired();

        builder.Property(cart => cart.RowVersion)
            .IsRowVersion();

        builder.HasIndex(cart => cart.UserId)
            .IsUnique()
            .HasDatabaseName("UX_Carts_UserId");

        builder.HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<Cart>(cart => cart.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
