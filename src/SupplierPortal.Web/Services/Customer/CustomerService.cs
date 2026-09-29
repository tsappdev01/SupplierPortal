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
    public record CustomerRow(int Id, string Number, string Name, string? FullName, string? Email,
        string Status, DateTime Updated, int? BusinessUnitId,
        string? Priority, int? AssignedToUserId, string? AssignedToUserName);

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
                MiddleName = c.Contact != null ? c.Contact.MiddleName : null,
                LastName = c.Contact != null ? c.Contact.LastName : null,
                Email = c.Contact != null ? c.Contact.Email : null,
                c.Status,
                Updated = c.UpdatedAt ?? c.CreatedAt,
                c.BusinessUnitId,
                c.Priority,
                c.AssignedToUserId,
                AssignedToUserName = c.AssignedToUser != null
                    ? (c.AssignedToUser.FullName ?? c.AssignedToUser.Username)
                    : null
            })
            .ToListAsync();

        return rows.Select(r => new CustomerRow(
            r.Id,
            string.IsNullOrWhiteSpace(r.Number) ? $"#{r.Id}" : r.Number!,
            string.IsNullOrWhiteSpace(r.LegalName) ? (r.CommercialName ?? "(unnamed draft)") : r.LegalName!,
            string.Join(" ", new[] { r.FirstName, r.MiddleName, r.LastName }.Where(p => !string.IsNullOrWhiteSpace(p))),
            r.Email, r.Status, r.Updated, r.BusinessUnitId,
            r.Priority, r.AssignedToUserId, r.AssignedToUserName)).ToList();
    }

    public async Task<Customer?> GetAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        // Deliberately do NOT include Documents here: their Content byte[] column would be pulled into memory
        // on every edit-page open, which is slow. Callers that need documents load them separately, and the
        // edit page uses GetDocumentCountAsync for the tab badge.
        return await db.Customers
            .Include(c => c.Contact)
            .Include(c => c.BusinessUnit)
            .Include(c => c.VatGroupEntities.OrderBy(e => e.SortOrder))
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    /// <summary>Number of attached documents for a customer, without loading any file bytes (for the edit
    /// page's "Documents (n)" badge).</summary>
    public async Task<int> GetDocumentCountAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CustomerDocuments.CountAsync(d => d.CustomerId == id);
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
    /// values (via the config-driven form) onto the tracked entities returned by <see cref="GetAsync"/>.
    /// <paramref name="readOnlyAccountKeys"/> / <paramref name="readOnlyContactKeys"/> name fields on tabs the
    /// acting user may not edit (per-tab edit restriction on the internal form): those keep their current
    /// database values regardless of the payload, so a locked tab can't be changed even by a tampered save.</summary>
    /// <summary>Persists the edited customer (+ its 1:1 contact). Returns false when no customer row matches
    /// <paramref name="edited"/>.Id (so callers/gateways can surface a real failure instead of a false success).</summary>
    public async Task<bool> SaveAsync(Customer edited, string actor,
        IReadOnlySet<string>? readOnlyAccountKeys = null, IReadOnlySet<string>? readOnlyContactKeys = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var customer = await db.Customers
            .Include(c => c.Contact)
            .Include(c => c.VatGroupEntities)
            .FirstOrDefaultAsync(c => c.Id == edited.Id);
        if (customer is null) return false;

        // Capture the current (DB) values of any read-only fields before the copy so they can be restored.
        var acctOriginals = Capture(customer, readOnlyAccountKeys);
        var contactOriginals = customer.Contact is not null ? Capture(customer.Contact, readOnlyContactKeys) : [];

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

        // Restore any read-only (locked-tab) fields so they can't be changed by this save.
        Restore(customer, acctOriginals);
        Restore(customer.Contact, contactOriginals);

        // Reconcile the VAT-group child rows: replace the stored set with what the form submitted (kept in
        // edited.VatGroupEntities). When VAT Structure is not "VAT Group" the block is hidden, so the group is
        // cleared — mirroring how the conditional "Number of Entities" field is cleared. Skip reconciliation only
        // when VAT Structure itself is on a locked (read-only) tab, so a locked Business tab can't be wiped.
        var vatStructureLocked = readOnlyAccountKeys?.Contains(nameof(Customer.VATStructure)) == true;
        if (!vatStructureLocked)
        {
            var isVatGroup = string.Equals(customer.VATStructure, "VAT Group", StringComparison.OrdinalIgnoreCase);
            db.CustomerVatGroupEntities.RemoveRange(customer.VatGroupEntities);
            customer.VatGroupEntities.Clear();
            if (isVatGroup)
            {
                var order = 0;
                foreach (var e in edited.VatGroupEntities ?? [])
                {
                    // Skip fully-blank rows so an empty "Add Row" doesn't persist noise.
                    if (string.IsNullOrWhiteSpace(e.BusinessLegalName) && string.IsNullOrWhiteSpace(e.Address)
                        && string.IsNullOrWhiteSpace(e.EInvoicingWave) && string.IsNullOrWhiteSpace(e.TrnNumber)
                        && string.IsNullOrWhiteSpace(e.TinNumber) && string.IsNullOrWhiteSpace(e.BusinessLocationType))
                    {
                        continue;
                    }
                    customer.VatGroupEntities.Add(new CustomerVatGroupEntity
                    {
                        CustomerId = customer.Id,
                        BusinessLegalName = e.BusinessLegalName,
                        Address = e.Address,
                        EInvoicingWave = e.EInvoicingWave,
                        TrnNumber = e.TrnNumber,
                        TinNumber = e.TinNumber,
                        BusinessLocationType = e.BusinessLocationType,
                        SortOrder = order++
                    });
                }
            }
        }

        customer.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await audit.LogAsync(actor, AuditAction.Update, "Customer", customer.Id, "Updated customer record");
        return true;
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

    // Reflection helpers for the per-tab read-only guard: capture/restore named scalar fields on an entity.
    private static Dictionary<string, object?> Capture(object entity, IReadOnlySet<string>? keys)
    {
        var captured = new Dictionary<string, object?>();
        if (keys is null || keys.Count == 0) return captured;
        foreach (var key in keys)
        {
            var prop = entity.GetType().GetProperty(key);
            if (prop is not null && prop.CanRead && prop.CanWrite) captured[key] = prop.GetValue(entity);
        }
        return captured;
    }

    private static void Restore(object entity, Dictionary<string, object?> originals)
    {
        foreach (var (key, val) in originals)
        {
            var prop = entity.GetType().GetProperty(key);
            if (prop is not null && prop.CanWrite) prop.SetValue(entity, val);
        }
    }

    /// <summary>The customer's display number and name, for naming the documents zip (mirrors the supplier
    /// "Number_Name.zip"). Number falls back Customer Code → Customer Id → Baan Ref; name falls back Legal →
    /// Commercial.</summary>
    public async Task<(string? Number, string? Name)> GetNumberAndNameAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var row = await db.Customers.Where(c => c.Id == id)
            .Select(c => new { c.CustomerCode, c.CustomerId, c.BaanRef, c.LegalName, c.CommercialName })
            .FirstOrDefaultAsync();
        if (row is null) return (null, null);
        var number = new[] { row.CustomerCode, row.CustomerId, row.BaanRef }
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        var name = new[] { row.LegalName, row.CommercialName }
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        return (number, name);
    }

    /// <summary>Sets the customer's work priority (P1..P4, or null to clear) from the dashboard.</summary>
    public async Task SetPriorityAsync(int id, string? priority, string actor)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id);
        if (customer is null) return;
        customer.Priority = string.IsNullOrWhiteSpace(priority) ? null : priority;
        await db.SaveChangesAsync();
        await audit.LogAsync(actor, AuditAction.Update, "Customer", id, $"Customer priority → {customer.Priority ?? "(none)"}");
    }

    /// <summary>Assigns the customer to an internal user (owner), or clears it when userId is null. Returns
    /// the assigned user's display name (or null) so the caller can refresh the row without a full reload.</summary>
    public async Task<string?> AssignAsync(int id, int? userId, string actor)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id);
        if (customer is null) return null;
        customer.AssignedToUserId = userId;
        await db.SaveChangesAsync();

        string? name = null;
        if (userId is int uid)
        {
            var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid);
            name = u is null ? null : (u.FullName ?? u.Username);
        }
        await audit.LogAsync(actor, AuditAction.Update, "Customer", id,
            userId is null ? "Customer unassigned" : $"Customer assigned to {name}");
        return name;
    }
}
