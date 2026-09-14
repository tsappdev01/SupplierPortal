using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SupplierPortal.Web.Data;
using SupplierPortal.Web.Models;

namespace SupplierPortal.Web.Services;

public class SupplierService(IDbContextFactory<ApplicationDbContext> dbFactory, AuditService auditService, IMemoryCache cache)
{
    // Reference data (lookups, banks) is shared across users and changes rarely, so it is cached to avoid
    // re-querying on every form open. TTL keeps admin changes appearing within a few minutes.
    private static readonly TimeSpan ReferenceCacheTtl = TimeSpan.FromMinutes(10);
    /// <summary>Fields excluded from change-tracking: identity/ownership metadata that isn't
    /// meaningful "what did the supplier/admin change" content, or that legitimately changes on
    /// every save (UpdatedAt/UpdatedBy) and would otherwise show up as noise on every update.</summary>
    private static readonly HashSet<string> DiffExcludedProperties =
    [
        nameof(Supplier.Id), nameof(Supplier.SupplierNumber), nameof(Supplier.BusinessUnitId),
        nameof(Supplier.IsDraft), nameof(Supplier.IsActive),
        nameof(Supplier.CreatedAt), nameof(Supplier.CreatedBy), nameof(Supplier.UpdatedAt), nameof(Supplier.UpdatedBy),
        // Submission/workflow artifacts, not editable fields — and the declaration text is far larger than
        // the change-tracking value column, so tracking it as a "field change" breaks the save.
        nameof(Supplier.DisclaimerText), nameof(Supplier.DisclaimerSignature),
        nameof(Supplier.DisclaimerAcknowledged), nameof(Supplier.DisclaimerAcknowledgedAt),
        nameof(Supplier.DisclaimerAcknowledgedBy), nameof(Supplier.ReviewRemarks), nameof(Supplier.StatusId)
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
            .Include(s => s.IntermediaryCountry)
            .Include(s => s.AssignedToUser);

    /// <summary>Pass null/empty businessUnitIds (Administrator, no filter) to see all business units' suppliers.</summary>
    public async Task<List<Supplier>> GetSuppliersAsync(
        IReadOnlyCollection<int>? businessUnitIds, string? supplierName = null, string? statusName = null,
        int? assignedToUserId = null, int? visibleToDepartmentId = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var query = IncludeLookups(db.Suppliers).AsNoTracking();
        if (businessUnitIds is { Count: > 0 })
        {
            query = query.Where(s => businessUnitIds.Contains(s.BusinessUnitId));
        }
        // Non-administrators are limited to suppliers assigned to them or owned by their department
        // (mirrors the dashboard list).
        if (assignedToUserId is not null || visibleToDepartmentId is not null)
        {
            query = query.Where(s =>
                (assignedToUserId != null && s.AssignedToUserId == assignedToUserId) ||
                (visibleToDepartmentId != null && s.DepartmentId == visibleToDepartmentId));
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
    public async Task<List<SupplierListItem>> GetSupplierListAsync(
        IReadOnlyCollection<int>? businessUnitIds, string? supplierName = null, int? assignedToUserId = null,
        int? visibleToDepartmentId = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var query = db.Suppliers.AsNoTracking().AsQueryable();
        if (businessUnitIds is { Count: > 0 })
        {
            query = query.Where(s => businessUnitIds.Contains(s.BusinessUnitId));
        }
        // Non-administrators see the suppliers assigned to them OR owned by their department (so a
        // department's members manage their department's suppliers). Administrators pass neither.
        if (assignedToUserId is not null || visibleToDepartmentId is not null)
        {
            query = query.Where(s =>
                (assignedToUserId != null && s.AssignedToUserId == assignedToUserId) ||
                (visibleToDepartmentId != null && s.DepartmentId == visibleToDepartmentId));
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
                Priority = s.Priority,
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
                AssignedToUserId = s.AssignedToUserId,
                AssignedToUserName = s.AssignedToUser != null
                    ? (s.AssignedToUser.FullName ?? s.AssignedToUser.Username)
                    : null,
                DepartmentId = s.DepartmentId,
                DepartmentName = s.Department != null ? s.Department.Name : null,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt,
                PendingSupplierChangeCount = db.SupplierFieldChanges.Count(
                    c => c.SupplierId == s.Id && !c.IsAcknowledged && c.SupplierAccessTokenId != null),
                DocumentCount = db.SupplierDocuments.Count(d => d.SupplierId == s.Id)
            })
            .ToListAsync();
    }

    /// <summary>Assigns (or, with a null userId, un-assigns) the supplier to an internal user from Manage
    /// Users. Records the change in the audit log.</summary>
    public async Task AssignAsync(int supplierId, int? userId, string actorUsername)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == supplierId);
        if (supplier is null || supplier.AssignedToUserId == userId)
        {
            return;
        }

        supplier.AssignedToUserId = userId;
        supplier.UpdatedAt = DateTime.UtcNow;
        supplier.UpdatedBy = actorUsername;
        await db.SaveChangesAsync();

        var detail = userId is null
            ? $"Unassigned supplier '{supplier.SupplierName}'"
            : $"Assigned supplier '{supplier.SupplierName}' to {(await db.Users.Where(u => u.Id == userId).Select(u => u.FullName ?? u.Username).FirstOrDefaultAsync()) ?? "user #" + userId}";
        await auditService.LogAsync(actorUsername, AuditAction.Update, "Supplier", supplierId, detail);
    }

    /// <summary>Assigns (or, with a null departmentId, un-assigns) the supplier to a department, so that
    /// department's users can manage it. Recorded in the audit log.</summary>
    public async Task AssignDepartmentAsync(int supplierId, int? departmentId, string actorUsername)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == supplierId);
        if (supplier is null || supplier.DepartmentId == departmentId)
        {
            return;
        }

        supplier.DepartmentId = departmentId;
        supplier.UpdatedAt = DateTime.UtcNow;
        supplier.UpdatedBy = actorUsername;
        await db.SaveChangesAsync();

        var detail = departmentId is null
            ? $"Removed department from supplier '{supplier.SupplierName}'"
            : $"Set department of supplier '{supplier.SupplierName}' to {(await db.Departments.Where(d => d.Id == departmentId).Select(d => d.Name).FirstOrDefaultAsync()) ?? "#" + departmentId}";
        await auditService.LogAsync(actorUsername, AuditAction.Update, "Supplier", supplierId, detail);
    }

    /// <summary>Assign (or clear, when <paramref name="departmentId"/> is null) the department of many suppliers
    /// at once. Only suppliers whose department actually changes are updated. Returns the number changed.</summary>
    public async Task<int> BulkAssignDepartmentAsync(IReadOnlyCollection<int> supplierIds, int? departmentId, string actorUsername)
    {
        if (supplierIds.Count == 0)
        {
            return 0;
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        var suppliers = await db.Suppliers
            .Where(s => supplierIds.Contains(s.Id) && s.DepartmentId != departmentId)
            .ToListAsync();
        if (suppliers.Count == 0)
        {
            return 0;
        }

        var now = DateTime.UtcNow;
        foreach (var supplier in suppliers)
        {
            supplier.DepartmentId = departmentId;
            supplier.UpdatedAt = now;
            supplier.UpdatedBy = actorUsername;
        }
        await db.SaveChangesAsync();

        var departmentName = departmentId is null
            ? null
            : await db.Departments.Where(d => d.Id == departmentId).Select(d => d.Name).FirstOrDefaultAsync();
        foreach (var supplier in suppliers)
        {
            var detail = departmentId is null
                ? $"Removed department from supplier '{supplier.SupplierName}'"
                : $"Set department of supplier '{supplier.SupplierName}' to {departmentName ?? "#" + departmentId}";
            await auditService.LogAsync(actorUsername, AuditAction.Update, "Supplier", supplier.Id, detail);
        }

        return suppliers.Count;
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

    /// <summary>Lightweight lookup of just a supplier's name (e.g. to name a download), avoiding the full
    /// entity graph that <see cref="GetByIdAsync"/> loads.</summary>
    public async Task<string?> GetNameAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Suppliers.Where(s => s.Id == id).Select(s => s.SupplierName).FirstOrDefaultAsync();
    }

    public async Task<Supplier?> GetByIdAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        // Split query: the three child collections combined with the many reference includes would
        // otherwise form one large cartesian join. Splitting keeps a single-supplier load fast.
        return await IncludeLookups(db.Suppliers)
            .Include(s => s.Contacts)
            .Include(s => s.BankAccounts)
            .Include(s => s.VatGroupEntities)
            .AsSplitQuery()
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<List<LookupValue>> GetLookupsAsync(LookupCategory category)
    {
        return (await cache.GetOrCreateAsync($"ref:lookups:{category}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = ReferenceCacheTtl;
            await using var db = await dbFactory.CreateDbContextAsync();
            return await db.LookupValues
                .Where(l => l.Category == category && l.IsActive)
                .OrderBy(l => l.DisplayName)
                .ToListAsync();
        }))!;
    }

    public async Task<List<Bank>> GetBanksAsync()
    {
        return (await cache.GetOrCreateAsync("ref:banks", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = ReferenceCacheTtl;
            await using var db = await dbFactory.CreateDbContextAsync();
            return await db.Banks.Include(b => b.Branches).OrderBy(b => b.Name).ToListAsync();
        }))!;
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

    public async Task UpdateAsync(Supplier updated, string updatedBy, int? accessTokenId = null, bool markComplete = false)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var supplier = await IncludeLookups(db.Suppliers)
            .Include(s => s.Contacts)
            .Include(s => s.BankAccounts)
            .Include(s => s.VatGroupEntities)
            .FirstAsync(s => s.Id == updated.Id);
        var before = DiffableProperties.ToDictionary(p => p.Name, p => FormatDisplayValue(supplier, p));

        // Fields hidden from suppliers are only hidden client-side, so protect them: on a supplier
        // self-service save, capture their current values and restore them after the copy so a
        // supplier can't change a locked field (e.g. via browser dev tools).
        var protectedOriginals = new Dictionary<string, object?>();
        if (accessTokenId is not null)
        {
            var hiddenKeys = await db.SupplierFieldSettings
                .Where(f => !f.IsVisibleToSupplier)
                .Select(f => f.FieldKey)
                .ToListAsync();
            foreach (var key in hiddenKeys)
            {
                var prop = typeof(Supplier).GetProperty(key);
                if (prop is not null && prop.CanWrite)
                {
                    protectedOriginals[key] = prop.GetValue(supplier);
                }
            }
        }

        supplier.SupplierNumber = updated.SupplierNumber;
        supplier.Priority = updated.Priority;
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

        // Fields added after this method was first written — copy them too so supplier/admin edits to
        // them actually persist (previously they were silently dropped on save).
        supplier.VatRegistered = updated.VatRegistered;
        supplier.VatRegistrationTypeId = updated.VatRegistrationTypeId;
        supplier.NumberOfVatGroupEntities = updated.NumberOfVatGroupEntities;
        supplier.VatGroupEntityNames = updated.VatGroupEntityNames;
        supplier.SupplierTin = updated.SupplierTin;
        supplier.BusinessPartnerTypeId = updated.BusinessPartnerTypeId;
        supplier.BusinessLocationTypeId = updated.BusinessLocationTypeId;
        supplier.EInvoicingWaveId = updated.EInvoicingWaveId;
        supplier.TradeLicenseIssueDate = updated.TradeLicenseIssueDate;
        supplier.TradeLicenseExpiryDate = updated.TradeLicenseExpiryDate;
        supplier.TradeLicenseIssuingBodyId = updated.TradeLicenseIssuingBodyId;
        supplier.IssuingBodyIfOthers = updated.IssuingBodyIfOthers;
        supplier.LicenseType = updated.LicenseType;
        supplier.CityText = updated.CityText;
        supplier.AddressPurposeOrdering = updated.AddressPurposeOrdering;
        supplier.AddressPurposeRemitTo = updated.AddressPurposeRemitTo;
        supplier.AddressPurposeRfqBidding = updated.AddressPurposeRfqBidding;
        supplier.MiddleName = updated.MiddleName;
        supplier.LastName = updated.LastName;
        supplier.MobileCountryCodeId = updated.MobileCountryCodeId;
        supplier.MobileAreaCode = updated.MobileAreaCode;
        supplier.Mobile = updated.Mobile;
        supplier.BusinessPhoneCountryCodeId = updated.BusinessPhoneCountryCodeId;
        supplier.BusinessPhoneAreaCode = updated.BusinessPhoneAreaCode;
        supplier.BusinessPhone = updated.BusinessPhone;
        supplier.AccountEndDate = updated.AccountEndDate;
        supplier.IntermediaryAccountApplicable = updated.IntermediaryAccountApplicable;
        supplier.IntermediaryCountryId = updated.IntermediaryCountryId;
        supplier.IntermediaryBankName = updated.IntermediaryBankName;
        supplier.IntermediaryAccountNumber = updated.IntermediaryAccountNumber;
        supplier.IntermediaryBranchNumber = updated.IntermediaryBranchNumber;
        supplier.IntermediaryIban = updated.IntermediaryIban;
        supplier.IntermediaryBicSwiftCode = updated.IntermediaryBicSwiftCode;

        // Self-service disclaimer acknowledgement recorded with the submission.
        supplier.DisclaimerAcknowledged = updated.DisclaimerAcknowledged;
        supplier.DisclaimerAcknowledgedAt = updated.DisclaimerAcknowledgedAt;
        supplier.DisclaimerAcknowledgedBy = updated.DisclaimerAcknowledgedBy;
        supplier.DisclaimerText = updated.DisclaimerText;
        supplier.DisclaimerSignature = updated.DisclaimerSignature;

        supplier.IsDraft = updated.IsDraft;

        // Restore any fields hidden from suppliers so a self-service save can never change a locked
        // field (the client only hides them; the server is the real guard).
        foreach (var (key, val) in protectedOriginals)
        {
            var prop = typeof(Supplier).GetProperty(key);
            if (prop is not null && prop.CanWrite)
            {
                prop.SetValue(supplier, val);
            }
        }

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

        // Reconcile VAT group entities: update rows still present, add new ones, drop removed ones.
        var incomingEntities = updated.VatGroupEntities ?? new List<SupplierVatGroupEntity>();
        var incomingEntityIds = incomingEntities.Where(e => e.Id != 0).Select(e => e.Id).ToHashSet();
        supplier.VatGroupEntities.RemoveAll(existing => existing.Id != 0 && !incomingEntityIds.Contains(existing.Id));
        foreach (var inc in incomingEntities)
        {
            var target = inc.Id != 0 ? supplier.VatGroupEntities.FirstOrDefault(e => e.Id == inc.Id) : null;
            if (target is null)
            {
                target = new SupplierVatGroupEntity();
                supplier.VatGroupEntities.Add(target);
            }

            target.BusinessLegalName = inc.BusinessLegalName;
            target.Address = inc.Address;
            target.EInvoicingWaveId = inc.EInvoicingWaveId;
            target.TrnNumber = inc.TrnNumber;
            target.TinNumber = inc.TinNumber;
            target.BusinessLocationTypeId = inc.BusinessLocationTypeId;
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
        else if (markComplete)
        {
            // A supplier self-service *submission* is treated as final: the record is auto-marked
            // "Completed" with no further review step. Intermediate saves as the supplier steps
            // through the wizard (markComplete == false) must not flip the status.
            supplier.StatusId = await ResolveStatusIdAsync(db, SupplierStatuses.Complete) ?? supplier.StatusId;
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
                existing.NewValue = CapChangeValue(newValue);
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
                    OldValue = CapChangeValue(oldValue),
                    NewValue = CapChangeValue(newValue),
                    ChangedAt = DateTime.UtcNow,
                    ChangedBy = changedBy
                });
            }
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Caps a change-tracking value to the SupplierFieldChange.OldValue/NewValue column length
    /// (500) so a long field value (e.g. a multi-line list) can never overflow and break the save.</summary>
    private static string? CapChangeValue(string? value) =>
        value is { Length: > 500 } ? value[..499] + "…" : value;

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
