using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Models;

namespace SupplierPortal.Web.Data;

/// <summary>The shared framework tables (identity, business units, departments, lookups, audit) that the
/// cross-cutting services in SupplierPortal.Shared read and write. Implemented by each application's own
/// <see cref="DbContext"/> — the main <c>ApplicationDbContext</c> and the separate Landlord context — so the
/// same services work against any of them regardless of which database they point at.</summary>
public interface IAppDbContext : IAsyncDisposable, IDisposable
{
    DbSet<User> Users { get; }
    DbSet<BusinessUnit> BusinessUnits { get; }
    DbSet<Department> Departments { get; }
    DbSet<LookupValue> LookupValues { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
