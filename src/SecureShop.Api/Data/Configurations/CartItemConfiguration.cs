using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecureShop.Api.Domain.Entities;

namespace SecureShop.Api.Data.Configurations;

public sealed class CartItemConfiguration
    : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable(
            "CartItems",
            tableBuilder => tableBuilder.HasCheckConstraint(
                "CK_CartItems_Quantity_Range",
                "[Quantity] BETWEEN 1 AND 99"));

        builder.HasKey(item => item.Id);

        builder.Property(item => item.Id)
            .ValueGeneratedNever();

        builder.Property(item => item.CartId)
            .IsRequired();

        builder.Property(item => item.ProductId)
            .IsRequired();

        builder.Property(item => item.Quantity)
            .IsRequired();

        builder.Property(item => item.CreatedAtUtc)
            .HasPrecision(0)
            .IsRequired();

        builder.Property(item => item.RowVersion)
            .IsRowVersion();

        builder.HasIndex(item => new
        {
            item.CartId,
            item.ProductId
        })
            .IsUnique()
            .HasDatabaseName("UX_CartItems_CartId_ProductId");

        builder.HasOne(item => item.Cart)
            .WithMany(cart => cart.Items)
            .HasForeignKey(item => item.CartId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasOne(item => item.Product)
            .WithMany()
            .HasForeignKey(item => item.ProductId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();
    }
}
