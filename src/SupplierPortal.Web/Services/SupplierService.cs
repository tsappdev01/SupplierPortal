using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data;
using SupplierPortal.Web.Models;

namespace SupplierPortal.Web.Services;

public class SupplierService(IDbContextFactory<ApplicationDbContext> dbFactory, AuditService auditService)
{
    /// <summary>Fields excluded from change-tracking: identity/ownership metadata that isn't
    /// meaningful "what did the supplier/admin change" content, or that legitimately changes on
    /// every save (UpdatedAt/UpdatedBy) and would otherwise show up as noise on every update.</summary>
    private static readonly HashSet<string> DiffExcludedProperties =
    [
        nameof(Supplier.Id), nameof(Supplier.SupplierNumber), nameof(Supplier.BusinessUnitId),
        nameof(Supplier.IsDraft), nameof(Supplier.IsActive),
        nameof(Supplier.CreatedAt), nameof(Supplier.CreatedBy), nameof(Supplier.UpdatedAt), nameof(Supplier.UpdatedBy)
    ];

    private static readonly PropertyInfo[] DiffableProperties = typeof(Supplier).GetProperties()
        .Where(p => !DiffExcludedProperties.Contains(p.Name) && IsSimpleType(Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType))
        .ToArray();

    private static bool IsSimpleType(Type t) =>
        t.IsPrimitive || t == typeof(string) || t == typeof(DateTime) || t == typeof(decimal) || t == typeof(Guid);

    private static IQueryable<Supplier> IncludeLookups(IQueryable<Supplier> query) =>
        query
            .Include(s => s.BusinessUnit)
            .Include(s => s.TaxOrganizationType)
            .Include(s => s.SupplierType)
            .Include(s => s.TaxpayerCountry)
            .Include(s => s.CountryName)
            .Include(s => s.Country)
            .Include(s => s.BusinessRelationship)
            .Include(s => s.VatCode)
            .Include(s => s.PaymentMethod)
            .Include(s => s.PaymentTerms)
            .Include(s => s.Prefix)
            .Include(s => s.Currency)
            .Include(s => s.Bank)
            .Include(s => s.BankBranch)
            .Include(s => s.Status)
            .Include(s => s.PhoneCountryCode)
            .Include(s => s.ProcurementBu)
            .Include(s => s.ClientBu)
            .Include(s => s.BillToBu)
            .Include(s => s.SupplierSiteBu)
            .Include(s => s.City)
            .Include(s => s.CommunicationMethod)
            .Include(s => s.AccountCountryCode)
            .Include(s => s.AccountCurrencyCode)
            // Newer classification / VAT / license / mobile / intermediary lookups, needed by the
            // full Excel export so their display names resolve.
            .Include(s => s.VatRegistrationType)
            .Include(s => s.BusinessPartnerType)
            .Include(s => s.BusinessLocationType)
            .Include(s => s.EInvoicingWave)
            .Include(s => s.TradeLicenseIssuingBody)
            .Include(s => s.MobileCountryCode)
            .Include(s => s.BusinessPhoneCountryCode)
            .Include(s => s.IntermediaryCountry);

    /// <summary>Pass null/empty businessUnitIds (Administrator, no filter) to see all business units' suppliers.</summary>
    public async Task<List<Supplier>> GetSuppliersAsync(
        IReadOnlyCollection<int>? businessUnitIds, string? supplierName = null, string? statusName = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var query = IncludeLookups(db.Suppliers).AsNoTracking();
        if (businessUnitIds is { Count: > 0 })
        {
            query = query.Where(s => businessUnitIds.Contains(s.BusinessUnitId));
        }
        if (!string.IsNullOrWhiteSpace(supplierName))
        {
            query = query.Where(s => s.SupplierName.Contains(supplierName));
        }
        if (!string.IsNullOrWhiteSpace(statusName))
        {
            query = query.Where(s => s.Status != null && s.Status.DisplayName == statusName);
        }

        var list = await query.OrderBy(s => s.SupplierName).ToListAsync();

        // Collapse accidental duplicate supplier records (same Supplier Number) so exports/templates
        // show one row per supplier — keeping the most complete (then most recently updated) record.
        var deduped = list
            .Where(s => !string.IsNullOrWhiteSpace(s.SupplierNumber))
            .GroupBy(s => s.SupplierNumber.Trim().ToLowerInvariant())
            .Select(g => g.OrderByDescending(CompletenessScore)
                          .ThenByDescending(s => s.UpdatedAt ?? s.CreatedAt)
                          .First())
            .ToList();
        deduped.AddRange(list.Where(s => string.IsNullOrWhiteSpace(s.SupplierNumber)));
        return deduped.OrderBy(s => s.SupplierName).ToList();
    }

    /// <summary>Rough "how filled in" score used to choose which record to keep when the same Supplier
    /// Number appears more than once (duplicate cleanup).</summary>
    public static int CompletenessScore(Supplier s)
    {
        var score = 0;
        if (!s.IsDraft) score += 2;                                         // submitted beats draft
        if (s.TaxOrganizationTypeId is not null) score++;
        if (s.SupplierTypeId is not null) score++;
        if (s.TaxpayerCountryId is not null) score++;
        if (!string.IsNullOrWhiteSpace(s.TradeLicenseNumber)) score++;
        if (!string.IsNullOrWhiteSpace(s.AddressLine1)) score++;
        if (s.CountryId is not null) score++;
        if (!string.IsNullOrWhiteSpace(s.ContactEmail)) score++;
        if (s.BankId is not null) score++;
        if (!string.IsNullOrWhiteSpace(s.TaxRegistrationNumber)) score++;
        return score;
    }

    /// <summary>Slim projection for the Manage Supplier dashboard: selects only the handful of
    /// lookup names the list actually renders (2 joins) instead of materializing the full Supplier
    /// graph (~20 joins) that <see cref="GetSuppliersAsync"/> loads for edit/export use, which was
    /// the main cause of the list page loading slowly.</summary>
    public async Task<List<SupplierListItem>> GetSupplierListAsync(IReadOnlyCollection<int>? businessUnitIds, string? supplierName = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var query = db.Suppliers.AsNoTracking().AsQueryable();
        if (businessUnitIds is { Count: > 0 })
        {
            query = query.Where(s => businessUnitIds.Contains(s.BusinessUnitId));
        }
        if (!string.IsNullOrWhiteSpace(supplierName))
        {
            query = query.Where(s => s.SupplierName.Contains(supplierName));
        }

        return await query
            .OrderBy(s => s.SupplierName)
            .Select(s => new SupplierListItem
            {
                Id = s.Id,
                SupplierNumber = s.SupplierNumber,
                SupplierName = s.SupplierName,
                TaxOrganizationTypeName = s.TaxOrganizationType != null ? s.TaxOrganizationType.DisplayName : null,
                FirstName = s.FirstName,
                SupplierTypeName = s.SupplierType != null ? s.SupplierType.DisplayName : null,
                TaxRegistrationNumber = s.TaxRegistrationNumber,
                TradeLicenseNumber = s.TradeLicenseNumber,
                ContactEmail = s.ContactEmail,
                StatusName = s.Status != null ? s.Status.DisplayName : null,
                ReviewRemarks = s.ReviewRemarks,
                IsActive = s.IsActive,
                IsDraft = s.IsDraft,
                BusinessUnitId = s.BusinessUnitId,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt,
                PendingSupplierChangeCount = db.SupplierFieldChanges.Count(
                    c => c.SupplierId == s.Id && !c.IsAcknowledged && c.SupplierAccessTokenId != null),
                DocumentCount = db.SupplierDocuments.Count(d => d.SupplierId == s.Id)
            })
            .ToListAsync();
    }

    /// <summary>Field keys (Supplier property names) with an unacknowledged, supplier-originated
    /// change pending review - drives the green "edited by supplier" highlight in the form.</summary>
    public async Task<HashSet<string>> GetPendingSupplierEditedFieldsAsync(int supplierId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return (await db.SupplierFieldChanges
            .Where(c => c.SupplierId == supplierId && !c.IsAcknowledged && c.SupplierAccessTokenId != null)
            .Select(c => c.FieldKey)
            .ToListAsync())
            .ToHashSet();
    }

    public async Task<Supplier?> GetByIdAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await IncludeLookups(db.Suppliers)
            .Include(s => s.Contacts)
            .Include(s => s.BankAccounts)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<List<LookupValue>> GetLookupsAsync(LookupCategory category)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.LookupValues
            .Where(l => l.Category == category && l.IsActive)
            .OrderBy(l => l.DisplayName)
            .ToListAsync();
    }

    public async Task<List<Bank>> GetBanksAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Banks.Include(b => b.Branches).OrderBy(b => b.Name).ToListAsync();
    }

    /// <summary>Generates "{BusinessUnit.Code}-{6-digit sequence}", e.g. "DIPD-000123".</summary>
    public async Task<string> GenerateSupplierNumberAsync(int businessUnitId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var businessUnit = await db.BusinessUnits.FirstAsync(b => b.Id == businessUnitId);
        var count = await db.Suppliers.CountAsync(s => s.BusinessUnitId == businessUnitId);
        return $"{businessUnit.Code}-{(count + 1):D6}";
    }

    /// <summary>Resolves a workflow status LookupValue id by its well-known name (see
    /// <see cref="SupplierStatuses"/>). Returns null if the status row hasn't been seeded.</summary>
    private static async Task<int?> ResolveStatusIdAsync(ApplicationDbContext db, string statusName) =>
        (await db.LookupValues.FirstOrDefaultAsync(
            l => l.Category == LookupCategory.SupplierStatus && l.DisplayName == statusName))?.Id;

    public async Task<int> CreateAsync(Supplier supplier, string actorUsername)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        supplier.StatusId ??= await ResolveStatusIdAsync(db, SupplierStatuses.New);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        await auditService.LogAsync(actorUsername, AuditAction.Create, "Supplier", supplier.Id, $"Created supplier '{supplier.SupplierName}'");

        return supplier.Id;
    }

    /// <summary>Marks a supplier's workflow status Complete (business has finished reviewing).</summary>
    public async Task MarkCompleteAsync(int supplierId, string actorUsername)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == supplierId);
        if (supplier is null)
        {
            return;
        }

        supplier.StatusId = await ResolveStatusIdAsync(db, SupplierStatuses.Complete) ?? supplier.StatusId;
        supplier.UpdatedAt = DateTime.UtcNow;
        supplier.UpdatedBy = actorUsername;

        // Completing the review expires any outstanding self-service link (and active OTP session) so
        // the supplier can no longer change their record - until the business sends a new notification,
        // which issues a fresh link and moves the status back to "Awaiting Supplier Response".
        var now = DateTime.UtcNow;
        var activeTokens = await db.SupplierAccessTokens
            .Where(t => t.SupplierId == supplierId && t.ExpiresAt > now)
            .ToListAsync();
        foreach (var token in activeTokens)
        {
            token.ExpiresAt = now;
            token.SessionExpiresAt = now;
        }

        await db.SaveChangesAsync();

        await auditService.LogAsync(actorUsername, AuditAction.Update, "Supplier", supplierId, $"Marked supplier '{supplier.SupplierName}' Complete");
    }

    /// <summary>MDM rejects a supplier (typically after re-reviewing a Completed record), capturing
    /// review remarks. Status moves to "Rejected" and the record returns to the internal owner, who can
    /// resubmit it to the supplier for correction.</summary>
    public async Task RejectAsync(int supplierId, string remarks, string actorUsername)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == supplierId);
        if (supplier is null)
        {
            return;
        }

        supplier.StatusId = await ResolveStatusIdAsync(db, SupplierStatuses.Rejected) ?? supplier.StatusId;
        supplier.ReviewRemarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();
        supplier.UpdatedAt = DateTime.UtcNow;
        supplier.UpdatedBy = actorUsername;

        await db.SaveChangesAsync();

        await auditService.LogAsync(actorUsername, AuditAction.Update, "Supplier", supplierId,
            $"Rejected supplier '{supplier.SupplierName}': {supplier.ReviewRemarks}");
    }

    public async Task UpdateAsync(Supplier updated, string updatedBy, int? accessTokenId = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var supplier = await IncludeLookups(db.Suppliers)
            .Include(s => s.Contacts)
            .Include(s => s.BankAccounts)
            .FirstAsync(s => s.Id == updated.Id);
        var before = DiffableProperties.ToDictionary(p => p.Name, p => FormatDisplayValue(supplier, p));

        supplier.SupplierNumber = updated.SupplierNumber;
        supplier.SupplierName = updated.SupplierName;
        supplier.Email = updated.Email;
        supplier.TaxOrganizationTypeId = updated.TaxOrganizationTypeId;
        supplier.SupplierTypeId = updated.SupplierTypeId;
        supplier.TaxpayerCountryId = updated.TaxpayerCountryId;
        supplier.CountryNameId = updated.CountryNameId;
        supplier.TaxRegistrationNumber = updated.TaxRegistrationNumber;
        supplier.TradeLicenseNumber = updated.TradeLicenseNumber;
        supplier.DunsNumber = updated.DunsNumber;
        supplier.CorporateWebSite = updated.CorporateWebSite;
        supplier.BusinessRelationshipId = updated.BusinessRelationshipId;
        supplier.IsOneTimeSupplier = updated.IsOneTimeSupplier;

        supplier.AddressName = updated.AddressName;
        supplier.AddressLine1 = updated.AddressLine1;
        supplier.AddressLine2 = updated.AddressLine2;
        supplier.CityId = updated.CityId;
        supplier.State = updated.State;
        supplier.CountryId = updated.CountryId;
        supplier.PostalCode = updated.PostalCode;
        supplier.RemittanceEmail = updated.RemittanceEmail;

        supplier.PrefixId = updated.PrefixId;
        supplier.FirstName = updated.FirstName;
        supplier.AdministrativeContact = updated.AdministrativeContact;
        supplier.ContactEmail = updated.ContactEmail;
        supplier.PhoneCountryCodeId = updated.PhoneCountryCodeId;
        supplier.PhoneAreaCode = updated.PhoneAreaCode;
        supplier.Phone = updated.Phone;

        supplier.SupplierSiteBuId = updated.SupplierSiteBuId;
        supplier.ProcurementBuId = updated.ProcurementBuId;
        supplier.ClientBuId = updated.ClientBuId;
        supplier.BillToBuId = updated.BillToBuId;
        supplier.CommunicationMethodId = updated.CommunicationMethodId;
        supplier.CommunicationEmail = updated.CommunicationEmail;
        supplier.IsRfqOrBidding = updated.IsRfqOrBidding;
        supplier.IsOrdering = updated.IsOrdering;
        supplier.IsSourcingOnly = updated.IsSourcingOnly;
        supplier.IsPurchasing = updated.IsPurchasing;
        supplier.IsPay = updated.IsPay;
        supplier.IsPrimaryPay = updated.IsPrimaryPay;

        supplier.PaymentMethodId = updated.PaymentMethodId;
        supplier.PaymentTermsId = updated.PaymentTermsId;
        supplier.PaymentMethodCode = updated.PaymentMethodCode;
        supplier.RemitAdviceEmail = updated.RemitAdviceEmail;
        supplier.VatCodeId = updated.VatCodeId;
        supplier.CurrencyId = updated.CurrencyId;
        supplier.BankId = updated.BankId;
        supplier.BankBranchId = updated.BankBranchId;
        supplier.AccountCountryCodeId = updated.AccountCountryCodeId;
        supplier.AccountName = updated.AccountName;
        supplier.AccountNumber = updated.AccountNumber;
        supplier.AccountCurrencyCodeId = updated.AccountCurrencyCodeId;
        supplier.SwiftCode = updated.SwiftCode;
        supplier.Iban = updated.Iban;
        supplier.AccountStartDate = updated.AccountStartDate;
        supplier.AllowInternationalPayments = updated.AllowInternationalPayments;

        supplier.IsDraft = updated.IsDraft;

        // Reconcile additional contacts: update rows still present, add new ones, drop removed ones.
        var incomingContacts = updated.Contacts ?? new List<SupplierContact>();
        var incomingIds = incomingContacts.Where(c => c.Id != 0).Select(c => c.Id).ToHashSet();
        supplier.Contacts.RemoveAll(existing => existing.Id != 0 && !incomingIds.Contains(existing.Id));
        foreach (var inc in incomingContacts)
        {
            var target = inc.Id != 0 ? supplier.Contacts.FirstOrDefault(c => c.Id == inc.Id) : null;
            if (target is null)
            {
                target = new SupplierContact();
                supplier.Contacts.Add(target);
            }

            target.PrefixId = inc.PrefixId;
            target.FirstName = inc.FirstName;
            target.MiddleName = inc.MiddleName;
            target.LastName = inc.LastName;
            target.AdministrativeContact = inc.AdministrativeContact;
            target.ContactEmail = inc.ContactEmail;
            target.PhoneCountryCodeId = inc.PhoneCountryCodeId;
            target.PhoneAreaCode = inc.PhoneAreaCode;
            target.Phone = inc.Phone;
            target.MobileCountryCodeId = inc.MobileCountryCodeId;
            target.MobileAreaCode = inc.MobileAreaCode;
            target.Mobile = inc.Mobile;
            target.SortOrder = inc.SortOrder;
        }

        // Reconcile additional bank accounts: update rows still present, add new ones, drop removed ones.
        var incomingAccounts = updated.BankAccounts ?? new List<SupplierBankAccount>();
        var incomingAccountIds = incomingAccounts.Where(a => a.Id != 0).Select(a => a.Id).ToHashSet();
        supplier.BankAccounts.RemoveAll(existing => existing.Id != 0 && !incomingAccountIds.Contains(existing.Id));
        foreach (var inc in incomingAccounts)
        {
            var target = inc.Id != 0 ? supplier.BankAccounts.FirstOrDefault(a => a.Id == inc.Id) : null;
            if (target is null)
            {
                target = new SupplierBankAccount();
                supplier.BankAccounts.Add(target);
            }

            target.BankId = inc.BankId;
            target.BankBranchId = inc.BankBranchId;
            target.AccountCountryCodeId = inc.AccountCountryCodeId;
            target.AccountName = inc.AccountName;
            target.AccountNumber = inc.AccountNumber;
            target.AccountCurrencyCodeId = inc.AccountCurrencyCodeId;
            target.SwiftCode = inc.SwiftCode;
            target.Iban = inc.Iban;
            target.AccountStartDate = inc.AccountStartDate;
            target.AccountEndDate = inc.AccountEndDate;
            target.SortOrder = inc.SortOrder;
        }

        supplier.UpdatedAt = DateTime.UtcNow;
        supplier.UpdatedBy = updatedBy;

        // An admin (non-self-service) save means the admin has now seen this record - including
        // any green "edited by supplier" highlights - so clear the pending-review flag on prior
        // supplier-originated changes. Self-service saves (accessTokenId set) never acknowledge:
        // a supplier resubmitting can't be the one reviewing their own earlier edit.
        if (accessTokenId is null)
        {
            var pendingSupplierChanges = await db.SupplierFieldChanges
                .Where(c => c.SupplierId == supplier.Id && !c.IsAcknowledged && c.SupplierAccessTokenId != null)
                .ToListAsync();
            foreach (var change in pendingSupplierChanges)
            {
                change.IsAcknowledged = true;
                change.AcknowledgedAt = DateTime.UtcNow;
                change.AcknowledgedBy = updatedBy;
            }
        }
        else
        {
            // A supplier self-service submission moves the record to "For Review" so the business
            // knows there are supplier-made changes waiting to be checked.
            supplier.StatusId = await ResolveStatusIdAsync(db, SupplierStatuses.ForReview) ?? supplier.StatusId;
        }

        await db.SaveChangesAsync();

        // Reload with fresh lookup Includes so FK-id fields (e.g. TaxOrganizationTypeId) diff as
        // their new human-readable display name, not a raw stale/cached nav reference.
        var reloaded = await IncludeLookups(db.Suppliers).AsNoTracking().FirstAsync(s => s.Id == supplier.Id);
        await UpsertFieldChangesAsync(db, supplier.Id, before, reloaded, updatedBy, accessTokenId);

        await auditService.LogAsync(updatedBy, AuditAction.Update, "Supplier", supplier.Id, $"Updated supplier '{supplier.SupplierName}'");
    }

    /// <summary>Human-readable field label for change-tracking: strips the "Id" suffix from FK-id
    /// properties that resolve to a lookup nav property (e.g. "TaxOrganizationTypeId" -> "Tax
    /// Organization Type"), otherwise just splits the raw property name's camel case.</summary>
    private static string FormatDisplayLabel(string propertyName)
    {
        if (propertyName.EndsWith("Id", StringComparison.Ordinal))
        {
            var navProperty = typeof(Supplier).GetProperty(propertyName[..^2]);
            if (navProperty is not null && navProperty.PropertyType != typeof(int) && navProperty.PropertyType != typeof(int?))
            {
                return SplitCamelCase(propertyName[..^2]);
            }
        }

        return SplitCamelCase(propertyName);
    }

    /// <summary>Formats a Supplier property for change-tracking display: FK-id properties resolve
    /// to their lookup's DisplayName/Name via the Include-loaded nav property; everything else is
    /// the raw scalar value's string form.</summary>
    private static string? FormatDisplayValue(Supplier supplier, PropertyInfo idProperty)
    {
        if (idProperty.Name.EndsWith("Id", StringComparison.Ordinal))
        {
            var navProperty = typeof(Supplier).GetProperty(idProperty.Name[..^2]);
            if (navProperty is not null && navProperty.PropertyType != typeof(int) && navProperty.PropertyType != typeof(int?))
            {
                var navValue = navProperty.GetValue(supplier);
                if (navValue is not null)
                {
                    var nameProperty = navProperty.PropertyType.GetProperty("DisplayName") ?? navProperty.PropertyType.GetProperty("Name");
                    return nameProperty?.GetValue(navValue) as string;
                }
                return idProperty.GetValue(supplier) is null ? null : idProperty.GetValue(supplier)!.ToString();
            }
        }

        var value = idProperty.GetValue(supplier);
        return value switch
        {
            null => null,
            DateTime dt => dt.ToString("yyyy-MM-dd"),
            bool b => b ? "Yes" : "No",
            _ => value.ToString()
        };
    }

    private async Task UpsertFieldChangesAsync(
        ApplicationDbContext db, int supplierId, Dictionary<string, string?> before, Supplier after,
        string changedBy, int? accessTokenId)
    {
        foreach (var property in DiffableProperties)
        {
            var oldValue = before[property.Name];
            var newValue = FormatDisplayValue(after, property);
            if (string.Equals(oldValue, newValue, StringComparison.Ordinal))
            {
                continue;
            }

            var existing = await db.SupplierFieldChanges.FirstOrDefaultAsync(
                c => c.SupplierId == supplierId && c.FieldKey == property.Name && !c.IsAcknowledged);

            if (existing is not null)
            {
                existing.NewValue = newValue;
                existing.ChangedAt = DateTime.UtcNow;
                existing.ChangedBy = changedBy;
                existing.SupplierAccessTokenId = accessTokenId;
            }
            else
            {
                db.SupplierFieldChanges.Add(new SupplierFieldChange
                {
                    SupplierId = supplierId,
                    SupplierAccessTokenId = accessTokenId,
                    FieldKey = property.Name,
                    DisplayLabel = FormatDisplayLabel(property.Name),
                    OldValue = oldValue,
                    NewValue = newValue,
                    ChangedAt = DateTime.UtcNow,
                    ChangedBy = changedBy
                });
            }
        }

        await db.SaveChangesAsync();
    }

    private static string SplitCamelCase(string value) =>
        System.Text.RegularExpressions.Regex.Replace(value, "(?<!^)([A-Z])", " $1");

    public async Task<int> AddRangeAsync(IEnumerable<Supplier> suppliers)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var list = suppliers.ToList();

        // Bulk-uploaded suppliers start at "New" (not yet sent to the supplier for review).
        var newStatusId = await ResolveStatusIdAsync(db, SupplierStatuses.New);
        foreach (var supplier in list)
        {
            supplier.StatusId ??= newStatusId;
        }

        db.Suppliers.AddRange(list);
        await db.SaveChangesAsync();
        return list.Count;
    }
}
