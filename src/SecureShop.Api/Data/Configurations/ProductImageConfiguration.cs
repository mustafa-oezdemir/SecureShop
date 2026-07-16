using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecureShop.Api.Domain.Entities;

namespace SecureShop.Api.Data.Configurations;

public sealed class ProductImageConfiguration
    : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable(
            "ProductImages",
            tableBuilder => tableBuilder.HasCheckConstraint(
                "CK_ProductImages_SortOrder_Range",
                "[SortOrder] BETWEEN 0 AND 99"));

        builder.HasKey(image => image.Id);

        builder.Property(image => image.Id)
            .ValueGeneratedNever();

        builder.Property(image => image.ProductId)
            .IsRequired();

        builder.Property(image => image.ImageUrl)
            .HasMaxLength(500)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(image => image.AltText)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(image => image.SortOrder)
            .IsRequired();

        builder.Property(image => image.IsPrimary)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(image => image.CreatedAtUtc)
            .HasPrecision(0)
            .IsRequired();

        builder.HasIndex(image => new
        {
            image.ProductId,
            image.SortOrder
        })
            .IsUnique()
            .HasDatabaseName("UX_ProductImages_ProductId_SortOrder");

        builder.HasIndex(image => image.ProductId)
            .IsUnique()
            .HasFilter("[IsPrimary] = 1")
            .HasDatabaseName("UX_ProductImages_ProductId_Primary");

        builder.HasOne(image => image.Product)
            .WithMany(product => product.Images)
            .HasForeignKey(image => image.ProductId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();
    }
}
