using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data;
using SupplierPortal.Web.Models;
using SupplierPortal.Web.Models.Customer;

namespace SupplierPortal.Web.Services;

/// <summary>Bulk-imports customers from the "Customer Data Template" workbook. The template is one flat row
/// per customer: the account columns first (Baan Ref#, Customer Unique Number, then the account fields), two
/// blank separator columns, then the contact columns (Baan Ref#, Customer Unique Number, then the contact
/// fields). Columns are mapped positionally using the field config order, and existing rows are matched by
/// Customer Unique Number so a re-upload updates rather than duplicates.</summary>
public class CustomerImportService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    CustomerFieldConfigService fieldConfig,
    AuditService audit)
{
    public async Task<ImportResult> ImportAsync(Stream stream, int? businessUnitId, string uploadedBy)
    {
        var errors = new List<ImportRowError>();
        var warnings = new List<ImportRowError>();
        var success = 0;

        var config = await fieldConfig.GetAllAsync();
        // Only visible fields form the column layout, matching the generated upload template exactly
        // (hidden fields are excluded from both, so the two stay aligned).
        var accountKeys = config.Where(c => c.Section == "Account" && c.IsVisible).OrderBy(c => c.Sequence).Select(c => c.FieldKey).ToList();
        var contactKeys = config.Where(c => c.Section == "Contact" && c.IsVisible).OrderBy(c => c.Sequence).Select(c => c.FieldKey).ToList();

        // Column layout (1-based): 1 Baan Ref#, 2 Customer Unique Number, then account fields;
        // 2 blank; then contact Baan/Unique (2); then contact fields.
        const int accountFirstCol = 3;
        var contactFirstCol = accountFirstCol + accountKeys.Count + 2 /*blank*/ + 2 /*contact keys*/;

        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.First();
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = (await db.Customers.Include(c => c.Contact)
                .Where(c => c.CustomerId != null)
                .ToListAsync())
            .GroupBy(c => c.CustomerId!)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        for (var r = 2; r <= lastRow; r++)
        {
            var row = ws.Row(r);
            var baanRef = Cell(row, 1);
            var uniqueNo = Cell(row, 2);
            var legalName = accountKeys.Count > 0 ? Cell(row, accountFirstCol + accountKeys.IndexOf("LegalName")) : "";

            // Skip completely blank rows silently.
            if (string.IsNullOrWhiteSpace(baanRef) && string.IsNullOrWhiteSpace(uniqueNo) && string.IsNullOrWhiteSpace(legalName))
                continue;

            if (string.IsNullOrWhiteSpace(legalName))
            {
                errors.Add(new ImportRowError(r, "Legal Name is required."));
                continue;
            }

            try
            {
                // Upsert by Customer Unique Number.
                Customer customer;
                var isNew = true;
                if (!string.IsNullOrWhiteSpace(uniqueNo) && existing.TryGetValue(uniqueNo.Trim(), out var found))
                {
                    customer = found;
                    isNew = false;
                }
                else
                {
                    customer = new Customer { Status = CustomerStatuses.NotSubmitted, CreatedBy = uploadedBy };
                }

                customer.BaanRef = Trim(baanRef);
                customer.CustomerId = Trim(uniqueNo);
                if (businessUnitId is int bu) customer.BusinessUnitId = bu;

                for (var i = 0; i < accountKeys.Count; i++)
                    CustomerFieldAccessor.SetString(customer, accountKeys[i], Cell(row, accountFirstCol + i));

                customer.Contact ??= new CustomerContact();
                for (var i = 0; i < contactKeys.Count; i++)
                    CustomerFieldAccessor.SetString(customer.Contact, contactKeys[i], Cell(row, contactFirstCol + i));

                customer.UpdatedAt = DateTime.UtcNow;

                if (isNew)
                {
                    db.Customers.Add(customer);
                    if (!string.IsNullOrWhiteSpace(customer.CustomerId))
                        existing[customer.CustomerId!] = customer;
                }
                success++;
            }
            catch (Exception ex)
            {
                errors.Add(new ImportRowError(r, ex.Message));
            }
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Surface the innermost SQL cause (EF's DbUpdateException hides it) instead of the generic
            // "See the inner exception" wrapper, and report zero imported since the batch rolled back.
            var current = ex;
            while (current.InnerException is not null) current = current.InnerException;
            errors.Add(new ImportRowError(0, $"Save failed: {current.Message}"));
            return new ImportResult(0, errors, warnings);
        }

        await audit.LogAsync(uploadedBy, AuditAction.Create, "Customer", null,
            $"Bulk-imported {success} customer(s) from Excel ({errors.Count} rejected)");

        return new ImportResult(success, errors, warnings);
    }

    private static string Cell(IXLRow row, int col)
    {
        var c = row.Cell(col);
        if (c.DataType == XLDataType.DateTime)
        {
            try { return c.GetDateTime().ToString("yyyy-MM-dd"); } catch { /* fall through */ }
        }
        return c.GetString();
    }

    private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
