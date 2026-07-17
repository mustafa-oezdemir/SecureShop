using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecureShop.Api.Domain.Entities;
using SecureShop.Api.Domain.Enums;
using SecureShop.Api.Security.Identity;

namespace SecureShop.Api.Data.Configurations;

public sealed class OrderConfiguration
    : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders", table =>
        {
            table.HasCheckConstraint(
                "CK_Orders_TotalAmount_NonNegative",
                "[TotalAmount] >= 0");
            table.HasCheckConstraint(
                "CK_Orders_Status_Range",
                $"[Status] BETWEEN {(int)OrderStatus.PendingApproval} AND {(int)OrderStatus.Cancelled}");
        });

        builder.HasKey(order => order.Id);

        builder.Property(order => order.OrderNumber)
            .HasMaxLength(32)
            .IsUnicode(false)
            .IsRequired();

        builder.HasIndex(order => order.OrderNumber)
            .IsUnique()
            .HasDatabaseName("UX_Orders_OrderNumber");

        builder.HasIndex(order => new
            {
                order.UserId,
                order.CreatedAtUtc
            })
            .HasDatabaseName("IX_Orders_UserId_CreatedAtUtc");

        builder.Property(order => order.RecipientName)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(order => order.AddressLine)
            .HasMaxLength(500)
            .IsRequired();
        builder.Property(order => order.PostalCode)
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(order => order.City)
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(order => order.Country)
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(order => order.Status)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(order => order.TotalAmount)
            .HasPrecision(18, 2)
            .IsRequired();
        builder.Property(order => order.CreatedAtUtc)
            .HasPrecision(0)
            .IsRequired();
        builder.Property(order => order.UpdatedAtUtc)
            .HasPrecision(0);
        builder.Property(order => order.CompletedAtUtc)
            .HasPrecision(0);
        builder.Property(order => order.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasMany(order => order.Items)
            .WithOne(item => item.Order)
            .HasForeignKey(item => item.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(order => order.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(order => order.ProcessedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
