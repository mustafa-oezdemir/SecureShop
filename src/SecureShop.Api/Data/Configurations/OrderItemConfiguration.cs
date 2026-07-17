using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecureShop.Api.Domain.Entities;

namespace SecureShop.Api.Data.Configurations;

public sealed class OrderItemConfiguration
    : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems", table =>
        {
            table.HasCheckConstraint(
                "CK_OrderItems_Quantity_Range",
                "[Quantity] BETWEEN 1 AND 99");
            table.HasCheckConstraint(
                "CK_OrderItems_UnitPrice_NonNegative",
                "[UnitPrice] >= 0");
            table.HasCheckConstraint(
                "CK_OrderItems_LineTotal_NonNegative",
                "[LineTotal] >= 0");
        });

        builder.HasKey(item => item.Id);

        builder.HasIndex(item => new
            {
                item.OrderId,
                item.ProductId
            })
            .IsUnique()
            .HasDatabaseName("UX_OrderItems_OrderId_ProductId");

        builder.Property(item => item.ProductName)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(item => item.Sku)
            .HasMaxLength(64)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(item => item.UnitPrice)
            .HasPrecision(18, 2)
            .IsRequired();
        builder.Property(item => item.Quantity)
            .IsRequired();
        builder.Property(item => item.LineTotal)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.HasOne(item => item.Product)
            .WithMany()
            .HasForeignKey(item => item.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
