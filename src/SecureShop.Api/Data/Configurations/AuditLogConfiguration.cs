using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecureShop.Api.Domain.Entities;
using SecureShop.Api.Security.Identity;

namespace SecureShop.Api.Data.Configurations;

public sealed class AuditLogConfiguration
    : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(log => log.Id);

        builder.HasIndex(log => log.CreatedAtUtc)
            .HasDatabaseName("IX_AuditLogs_CreatedAtUtc");
        builder.HasIndex(log => new
            {
                log.EntityType,
                log.EntityId
            })
            .HasDatabaseName("IX_AuditLogs_Entity");

        builder.Property(log => log.Action)
            .HasMaxLength(100)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(log => log.EntityType)
            .HasMaxLength(100)
            .IsUnicode(false)
            .IsRequired();
        builder.Property(log => log.EntityId)
            .HasMaxLength(100)
            .IsUnicode(false);
        builder.Property(log => log.DetailsJson)
            .HasMaxLength(4000);
        builder.Property(log => log.IpAddress)
            .HasMaxLength(64)
            .IsUnicode(false);
        builder.Property(log => log.CreatedAtUtc)
            .HasPrecision(0)
            .IsRequired();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(log => log.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
