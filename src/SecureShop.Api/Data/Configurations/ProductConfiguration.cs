using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecureShop.Api.Domain.Entities;

namespace SecureShop.Api.Data.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable(
            "Products",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "CK_Products_Price_NonNegative",
                    "[Price] >= 0");

                tableBuilder.HasCheckConstraint(
                    "CK_Products_StockQuantity_NonNegative",
                    "[StockQuantity] >= 0");
            });

        builder.HasKey(product => product.Id);

        builder.Property(product => product.Id)
            .ValueGeneratedNever();

        builder.Property(product => product.CategoryId)
            .IsRequired();

        builder.Property(product => product.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(product => product.Sku)
            .HasMaxLength(64)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(product => product.Description)
            .HasMaxLength(2000);

        builder.Property(product => product.Price)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(product => product.StockQuantity)
            .IsRequired();

        builder.Property(product => product.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(product => product.CreatedAtUtc)
            .HasPrecision(0)
            .IsRequired();

        builder.Property(product => product.UpdatedAtUtc)
            .HasPrecision(0);

        builder.Property(product => product.RowVersion)
            .IsRowVersion();

        builder.HasIndex(product => product.Sku)
            .IsUnique()
            .HasDatabaseName("UX_Products_Sku");

        builder.HasIndex(product => product.CategoryId)
            .HasDatabaseName("IX_Products_CategoryId");

        builder.HasIndex(product => new
        {
            product.IsActive,
            product.Name
        })
            .HasDatabaseName("IX_Products_IsActive_Name");
    }
}