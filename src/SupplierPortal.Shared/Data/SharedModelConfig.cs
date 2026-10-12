using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Models;

namespace SupplierPortal.Web.Data;

/// <summary>EF Core configuration for the shared framework entities (identity, business units, departments,
/// lookups, audit). Applied by every app's <see cref="DbContext"/> that implements <see cref="IAppDbContext"/>
/// — the main <c>ApplicationDbContext</c> and the separate Landlord context — so the shared tables are shaped
/// identically in each database. Keep this the single source of truth for shared-table mapping.</summary>
public static class SharedModelConfig
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasMany(u => u.BusinessUnits)
            .WithMany(b => b.Users)
            .UsingEntity<Dictionary<string, object>>(
                "UserBusinessUnit",
                j => j.HasOne<BusinessUnit>().WithMany().HasForeignKey("BusinessUnitId").OnDelete(DeleteBehavior.Cascade),
                j => j.HasOne<User>().WithMany().HasForeignKey("UserId").OnDelete(DeleteBehavior.Cascade),
                j =>
                {
                    j.HasKey("UserId", "BusinessUnitId");
                    j.ToTable("UserBusinessUnits");
                });

        modelBuilder.Entity<User>()
            .HasOne(u => u.CompanyBusinessUnit)
            .WithMany()
            .HasForeignKey(u => u.CompanyBusinessUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<BusinessUnit>()
            .HasIndex(b => b.Code)
            .IsUnique();

        modelBuilder.Entity<LookupValue>()
            .HasIndex(l => new { l.Category, l.Code })
            .IsUnique();
    }
}
