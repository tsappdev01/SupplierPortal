using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data;
using SupplierPortal.Web.Models;

namespace SupplierPortal.Leasing.Web.Data;

/// <summary>The Landlord application's own database context. It owns BOTH the shared framework tables
/// (identity, business units, departments, lookups, audit) — because the Landlord app runs against its own
/// separate database — AND (added in a later phase) the <c>leasing.*</c> tables. Implementing
/// <see cref="IAppDbContext"/> lets every shared service in SupplierPortal.Shared run against this context
/// unchanged.</summary>
public class LeasingAppDbContext(DbContextOptions<LeasingAppDbContext> options) : DbContext(options), IAppDbContext
{
    // ---- Shared framework tables (shaped by SharedModelConfig) ----
    public DbSet<User> Users => Set<User>();
    public DbSet<BusinessUnit> BusinessUnits => Set<BusinessUnit>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<LookupValue> LookupValues => Set<LookupValue>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // ---- Landlord (leasing.*) tables are added in the domain-move phase ----

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        SharedModelConfig.Apply(modelBuilder);
    }
}
