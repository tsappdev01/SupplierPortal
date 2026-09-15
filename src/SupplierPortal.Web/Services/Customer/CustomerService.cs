using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data;
using SupplierPortal.Web.Models;
using SupplierPortal.Web.Models.Customer;

namespace SupplierPortal.Web.Services;

/// <summary>CRUD + workflow for CST customer records. Owned by the Business Relation department. Records are
/// scoped to the user's business units (like suppliers), and move Draft → For Review → Completed (or Returned).</summary>
public class CustomerService(IDbContextFactory<ApplicationDbContext> dbFactory, AuditService audit)
{
    /// <summary>True when the customer with <paramref name="customerId"/> is visible to a caller scoped to
    /// <paramref name="scopeBusinessUnitIds"/> — the same rule as the dashboard list (empty scope = admin,
    /// sees all; otherwise the customer's BU must be null or in scope). Used by the document/download
    /// endpoints so a user cannot pull another business unit's customer KYC files by guessing ids.</summary>
    public async Task<bool> IsCustomerAccessibleAsync(int customerId, List<int> scopeBusinessUnitIds)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var q = db.Customers.AsNoTracking().Where(c => c.Id == customerId);
        if (scopeBusinessUnitIds.Count > 0)
            q = q.Where(c => c.BusinessUnitId == null || scopeBusinessUnitIds.Contains(c.BusinessUnitId.Value));
        return await q.AnyAsync();
    }

    /// <summary>One page of customers, filtered by search / status / business unit and scoped to the user's BUs.</summary>
    public async Task<(List<Customer> Items, int Total)> GetListAsync(
        string? search, string? status, int? businessUnitId, List<int> scopeBusinessUnitIds, int page, int pageSize)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var q = db.Customers.AsNoTracking().Include(c => c.BusinessUnit).AsQueryable();

        // Business-unit scope: users only see customers in their assigned BUs (records with no BU are visible to all).
        if (scopeBusinessUnitIds.Count > 0)
            q = q.Where(c => c.BusinessUnitId == null || scopeBusinessUnitIds.Contains(c.BusinessUnitId.Value));

        if (businessUnitId is int bu)
            q = q.Where(c => c.BusinessUnitId == bu);

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(c => c.Status == status);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(c =>
                (c.LegalName != null && c.LegalName.Contains(s)) ||
                (c.CommercialName != null && c.CommercialName.Contains(s)) ||
                (c.CustomerId != null && c.CustomerId.Contains(s)) ||
                (c.BaanRef != null && c.BaanRef.Contains(s)) ||
                (c.TRNNumber != null && c.TRNNumber.Contains(s)));
        }

        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
            .Skip(Math.Max(0, page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return (items, total);
    }

    /// <summary>All customers matching the current list filters (BU scope + business unit + status +
    /// search), with their contact, for export. No paging — the whole filtered set is returned.</summary>
    public async Task<List<Customer>> GetForExportAsync(
        string? search, string? status, int? businessUnitId, List<int> scopeBusinessUnitIds)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var q = db.Customers.AsNoTracking().Include(c => c.BusinessUnit).Include(c => c.Contact).AsQueryable();

        if (scopeBusinessUnitIds.Count > 0)
            q = q.Where(c => c.BusinessUnitId == null || scopeBusinessUnitIds.Contains(c.BusinessUnitId.Value));
        if (businessUnitId is int bu)
            q = q.Where(c => c.BusinessUnitId == bu);
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(c => c.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(c =>
                (c.LegalName != null && c.LegalName.Contains(s)) ||
                (c.CommercialName != null && c.CommercialName.Contains(s)) ||
                (c.CustomerId != null && c.CustomerId.Contains(s)) ||
                (c.BaanRef != null && c.BaanRef.Contains(s)) ||
                (c.TRNNumber != null && c.TRNNumber.Contains(s)));
        }

        return await q.OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt).ToListAsync();
    }

    /// <summary>A lightweight row for the Manage Customer dashboard (mirrors the supplier list columns).</summary>
    public record CustomerRow(int Id, string Number, string Name, string? FirstName, string? Email,
        string Status, DateTime Updated, int? BusinessUnitId);

    /// <summary>All customers the user may see (BU-scoped), projected for the dashboard grid + KPI tiles.</summary>
    public async Task<List<CustomerRow>> GetDashboardAsync(List<int> scopeBusinessUnitIds)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var q = db.Customers.AsNoTracking().Include(c => c.Contact).AsQueryable();
        if (scopeBusinessUnitIds.Count > 0)
            q = q.Where(c => c.BusinessUnitId == null || scopeBusinessUnitIds.Contains(c.BusinessUnitId.Value));

        var rows = await q
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                Number = c.CustomerId ?? c.BaanRef,
                c.LegalName,
                c.CommercialName,
                FirstName = c.Contact != null ? c.Contact.FirstName : null,
                Email = c.Contact != null ? c.Contact.Email : null,
                c.Status,
                Updated = c.UpdatedAt ?? c.CreatedAt,
                c.BusinessUnitId
            })
            .ToListAsync();

        return rows.Select(r => new CustomerRow(
            r.Id,
            string.IsNullOrWhiteSpace(r.Number) ? $"#{r.Id}" : r.Number!,
            string.IsNullOrWhiteSpace(r.LegalName) ? (r.CommercialName ?? "(unnamed draft)") : r.LegalName!,
            r.FirstName, r.Email, r.Status, r.Updated, r.BusinessUnitId)).ToList();
    }

    public async Task<Customer?> GetAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Customers
            .Include(c => c.Contact)
            .Include(c => c.Documents)
            .Include(c => c.BusinessUnit)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    /// <summary>Creates an empty draft customer (+ empty primary contact) and returns its id, so the BR user
    /// lands straight on the edit form.</summary>
    public async Task<int> CreateDraftAsync(string actor, int? businessUnitId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var customer = new Customer
        {
            Status = CustomerStatuses.NotSubmitted,
            BusinessUnitId = businessUnitId,
            CreatedBy = actor,
            CreatedAt = DateTime.UtcNow,
            Contact = new CustomerContact()
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        await audit.LogAsync(actor, AuditAction.Create, "Customer", customer.Id, "Created customer draft");
        return customer.Id;
    }

    /// <summary>Persists edits to the account + its primary contact. The caller has already applied the edited
    /// values (via the config-driven form) onto the tracked entities returned by <see cref="GetAsync"/>.</summary>
    public async Task SaveAsync(Customer edited, string actor)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var customer = await db.Customers.Include(c => c.Contact).FirstOrDefaultAsync(c => c.Id == edited.Id);
        if (customer is null) return;

        db.Entry(customer).CurrentValues.SetValues(edited);

        edited.Contact ??= new CustomerContact();
        if (customer.Contact is null)
        {
            customer.Contact = new CustomerContact { CustomerId = customer.Id };
            db.CustomerContacts.Add(customer.Contact);
        }
        // copy contact scalar values, preserving keys
        var keepId = customer.Contact.Id;
        var keepFk = customer.Id;
        db.Entry(customer.Contact).CurrentValues.SetValues(edited.Contact);
        customer.Contact.Id = keepId;
        customer.Contact.CustomerId = keepFk;

        customer.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await audit.LogAsync(actor, AuditAction.Update, "Customer", customer.Id, "Updated customer record");
    }

    public async Task SetStatusAsync(int id, string status, string actor)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id);
        if (customer is null) return;
        customer.Status = status;
        customer.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await audit.LogAsync(actor, AuditAction.Update, "Customer", id, $"Customer status → {status}");
    }
}
