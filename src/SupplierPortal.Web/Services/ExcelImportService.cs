using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data;
using SupplierPortal.Web.Models;

namespace SupplierPortal.Web.Services;

public record ImportRowError(int RowNumber, string Message);

public record ImportResult(int SuccessCount, List<ImportRowError> Errors, List<ImportRowError> Warnings);

public class ExcelImportService(
    IDbContextFactory<ApplicationDbContext> dbFactory, SupplierService supplierService)
{
    // Maps accepted header text (lower-cased, trimmed) to the field it feeds.
    private static readonly Dictionary<string, string> HeaderMap = new()
    {
        ["supplier number"] = "SupplierNumber",
        ["supplier name"] = "SupplierName",
        // "email"/"e-mail" are handled positionally below (the template repeats a bare "E-Mail"
        // header twice); these entries exist only so worksheet auto-selection still counts them.
        ["email"] = "ContactEmail",
        ["e-mail"] = "ContactEmail",
        ["tax organization type"] = "TaxOrganizationType",
        ["supplier type"] = "SupplierType",
        ["taxpayer country"] = "TaxpayerCountry",
        ["country name"] = "CountryName",
        ["tax registration number"] = "TaxRegistrationNumber",
        ["trade license number"] = "TradeLicenseNumber",
        ["d-u-n-s number"] = "DunsNumber",
        ["corporate web site"] = "CorporateWebSite",
        ["business relationship"] = "BusinessRelationship",
        ["one-time supplier"] = "IsOneTimeSupplier",
        ["address name"] = "AddressName",
        ["address line 1"] = "AddressLine1",
        ["address line 2"] = "AddressLine2",
        ["city"] = "City",
        ["state"] = "State",
        ["country"] = "Country",
        ["postal code"] = "PostalCode",
        ["remittance e-mail"] = "RemittanceEmail",
        ["prefix"] = "Prefix",
        ["first name"] = "FirstName",
        ["administrative contact"] = "AdministrativeContact",
        ["phone country code"] = "PhoneCountryCode",
        ["phone area code"] = "PhoneAreaCode",
        ["phone"] = "Phone",
        ["supplier site"] = "SupplierSite",
        ["procurement bu"] = "ProcurementBu",
        ["client bu"] = "ClientBu",
        ["bill-to bu"] = "BillToBu",
        ["communication method"] = "CommunicationMethod",
        ["rfq or bidding"] = "IsRfqOrBidding",
        ["ordering"] = "IsOrdering",
        ["sourcing only"] = "IsSourcingOnly",
        ["purchasing"] = "IsPurchasing",
        ["pay"] = "IsPay",
        ["primary pay"] = "IsPrimaryPay",
        ["payment method"] = "PaymentMethod",
        ["payment terms"] = "PaymentTerms",
        ["payment method code"] = "PaymentMethodCode",
        ["remit advice email"] = "RemitAdviceEmail",
        ["vat code"] = "VatCode",
        ["currency_code"] = "Currency",
        ["currency code"] = "Currency",
        ["currency"] = "Currency",
        ["bank name"] = "BankName",
        ["branch name"] = "BranchName",
        ["account country code"] = "AccountCountryCode",
        ["account name"] = "AccountName",
        ["account number"] = "AccountNumber",
        ["account currency code"] = "AccountCurrencyCode",
        ["swift code"] = "SwiftCode",
        ["swift code/bic code"] = "SwiftCode",
        ["iban"] = "Iban",
        ["account start date"] = "AccountStartDate",
        ["allow international payments"] = "AllowInternationalPayments"
    };

    public async Task<ImportResult> ImportAsync(Stream fileStream, int businessUnitId, string uploadedBy)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        // Bulk import of legacy/master data is intentionally lenient: only Supplier Number and
        // Supplier Name are hard-required. Every other field is best-effort - a value that isn't
        // recognized against a lookup is left blank and reported as a warning, never rejecting the
        // row. (The strict Mandatory field settings apply to the interactive registration wizard,
        // not to importing historical data that is usually incomplete.)
        var taxOrgTypes = await LoadLookupAsync(db, LookupCategory.TaxOrganizationType);
        var supplierTypes = await LoadLookupAsync(db, LookupCategory.SupplierType);
        var countries = await LoadLookupAsync(db, LookupCategory.Country, includeCode: true);
        var countriesByPhoneCode = await LoadCountriesByPhoneCodeAsync(db);
        var businessRelationships = await LoadLookupAsync(db, LookupCategory.BusinessRelationship);
        var provinces = await LoadLookupAsync(db, LookupCategory.Province);
        var vatCodes = await LoadLookupAsync(db, LookupCategory.VatCode, includeCode: true);
        var paymentMethods = await LoadLookupAsync(db, LookupCategory.PaymentMethod);
        var paymentTerms = await LoadLookupAsync(db, LookupCategory.PaymentTerms);
        var prefixes = await LoadLookupAsync(db, LookupCategory.Prefix);
        var communicationMethods = await LoadLookupAsync(db, LookupCategory.CommunicationMethod);
        var currencies = await LoadLookupAsync(db, LookupCategory.Currency, includeCode: true);
        var banks = await db.Banks.Include(b => b.Branches).ToListAsync();
        var businessUnits = await db.BusinessUnits.ToDictionaryAsync(b => b.Name.ToLowerInvariant(), b => b.Id);

        // Duplicate guard: a re-uploaded template must not create a second copy of a supplier that is
        // already in the system, nor of a supplier number repeated within the file. Both are reported
        // and the offending row is skipped.
        var existingNumbers = (await db.Suppliers
                .Select(s => s.SupplierNumber)
                .ToListAsync())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim().ToLowerInvariant())
            .ToHashSet();
        var seenNumbers = new HashSet<string>();

        // ClosedXML reads its input stream synchronously, which browser-uploaded IBrowserFile streams
        // (Blazor Server, streamed over SignalR) don't support. Buffer to a seekable MemoryStream first.
        using var bufferedStream = new MemoryStream();
        await fileStream.CopyToAsync(bufferedStream);
        bufferedStream.Position = 0;

        using var workbook = new XLWorkbook(bufferedStream);
        var worksheet = SelectDataWorksheet(workbook);
        var firstRow = worksheet.FirstRowUsed()
            ?? throw new InvalidOperationException("The uploaded file appears to be empty.");

        // The official template repeats the bare "E-Mail" header twice (once near the primary
        // contact's details, once near Communication Method) with no other distinguishing text, so a
        // plain text->field map can't tell them apart. Assign by occurrence order instead: 1st -> the
        // contact's email, 2nd -> the communication email.
        var columnMap = new Dictionary<int, string>();
        var emailOccurrence = 0;
        foreach (var cell in firstRow.CellsUsed())
        {
            var header = NormalizeHeader(cell.GetString());
            if (header is "email" or "e-mail")
            {
                emailOccurrence++;
                columnMap[cell.Address.ColumnNumber] = emailOccurrence == 1 ? "ContactEmail" : "CommunicationEmail";
                continue;
            }

            if (HeaderMap.TryGetValue(header, out var field))
            {
                columnMap[cell.Address.ColumnNumber] = field;
            }
        }

        var errors = new List<ImportRowError>();
        var warnings = new List<ImportRowError>();
        var suppliers = new List<Supplier>();

        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            var values = new Dictionary<string, string>();
            foreach (var (column, field) in columnMap)
            {
                values[field] = row.Cell(column).GetString().Trim();
            }

            var rowNumber = row.RowNumber();
            var rowErrors = new List<string>();
            var rowWarnings = new List<string>();

            values.TryGetValue("SupplierNumber", out var supplierNumber);
            values.TryGetValue("SupplierName", out var supplierName);
            values.TryGetValue("TaxRegistrationNumber", out var taxRegistrationNumber);

            if (string.IsNullOrWhiteSpace(supplierNumber)) rowErrors.Add("Supplier Number is required.");
            if (string.IsNullOrWhiteSpace(supplierName)) rowErrors.Add("Supplier Name is required.");

            // Skip duplicates (already in the system, or repeated earlier in this file).
            if (!string.IsNullOrWhiteSpace(supplierNumber))
            {
                var dupKey = supplierNumber.Trim().ToLowerInvariant();
                if (existingNumbers.Contains(dupKey))
                {
                    rowErrors.Add($"Supplier Number '{supplierNumber}' already exists — skipped as a duplicate.");
                }
                else if (!seenNumbers.Add(dupKey))
                {
                    rowErrors.Add($"Supplier Number '{supplierNumber}' appears more than once in the file — skipped as a duplicate.");
                }
            }

            // Every lookup below is optional on import: an unrecognized value is left blank and noted
            // as a warning (see ResolveLookup), so incomplete legacy data still imports.
            var taxOrgTypeId = ResolveLookup(values, "TaxOrganizationType", taxOrgTypes, required: false, rowErrors, rowWarnings);
            var supplierTypeId = ResolveLookup(values, "SupplierType", supplierTypes, required: false, rowErrors, rowWarnings);
            var taxpayerCountryId = ResolveLookup(values, "TaxpayerCountry", countries, required: false, rowErrors, rowWarnings);
            var countryNameId = ResolveLookup(values, "CountryName", countries, required: false, rowErrors, rowWarnings);
            var countryId = ResolveLookup(values, "Country", countries, required: false, rowErrors, rowWarnings);
            var businessRelationshipId = ResolveLookup(values, "BusinessRelationship", businessRelationships, required: false, rowErrors, rowWarnings);
            var cityId = ResolveLookup(values, "City", provinces, required: false, rowErrors, rowWarnings);
            var vatCodeId = ResolveLookup(values, "VatCode", vatCodes, required: false, rowErrors, rowWarnings);
            var paymentMethodId = ResolveLookup(values, "PaymentMethod", paymentMethods, required: false, rowErrors, rowWarnings);
            var paymentTermsId = ResolveLookup(values, "PaymentTerms", paymentTerms, required: false, rowErrors, rowWarnings);
            var prefixId = ResolveLookup(values, "Prefix", prefixes, required: false, rowErrors, rowWarnings);
            var communicationMethodId = ResolveLookup(values, "CommunicationMethod", communicationMethods, required: false, rowErrors, rowWarnings);
            var currencyId = ResolveLookup(values, "Currency", currencies, required: false, rowErrors, rowWarnings);
            var accountCountryCodeId = ResolveLookup(values, "AccountCountryCode", countries, required: false, rowErrors, rowWarnings);
            var accountCurrencyCodeId = ResolveLookup(values, "AccountCurrencyCode", currencies, required: false, rowErrors, rowWarnings);
            var phoneCountryCodeId = ResolveLookup(values, "PhoneCountryCode", countriesByPhoneCode, required: false, rowErrors, rowWarnings);
            var supplierSiteBuId = ResolveLookup(values, "SupplierSite", businessUnits, required: false, rowErrors, rowWarnings);
            var procurementBuId = ResolveLookup(values, "ProcurementBu", businessUnits, required: false, rowErrors, rowWarnings);
            var clientBuId = ResolveLookup(values, "ClientBu", businessUnits, required: false, rowErrors, rowWarnings);
            var billToBuId = ResolveLookup(values, "BillToBu", businessUnits, required: false, rowErrors, rowWarnings);
            var (bankId, bankBranchId) = ResolveBankBranch(values, banks, rowWarnings);

            values.TryGetValue("AddressLine2", out var addressLine2);
            values.TryGetValue("PostalCode", out var postalCode);
            values.TryGetValue("Phone", out var phone);

            if (rowErrors.Count > 0)
            {
                errors.Add(new ImportRowError(rowNumber, string.Join(" ", rowErrors)));
                continue;
            }

            if (rowWarnings.Count > 0)
            {
                warnings.Add(new ImportRowError(rowNumber, string.Join(" ", rowWarnings)));
            }

            suppliers.Add(new Supplier
            {
                SupplierNumber = supplierNumber!,
                SupplierName = supplierName!,
                TaxOrganizationTypeId = taxOrgTypeId,
                SupplierTypeId = supplierTypeId,
                TaxpayerCountryId = taxpayerCountryId,
                CountryNameId = countryNameId,
                TaxRegistrationNumber = taxRegistrationNumber ?? "",
                TradeLicenseNumber = GetOrNull(values, "TradeLicenseNumber"),
                DunsNumber = GetOrNull(values, "DunsNumber"),
                CorporateWebSite = GetOrNull(values, "CorporateWebSite"),
                BusinessRelationshipId = businessRelationshipId,
                IsOneTimeSupplier = ParseBool(values, "IsOneTimeSupplier"),

                AddressName = GetOrNull(values, "AddressName"),
                AddressLine1 = GetOrNull(values, "AddressLine1"),
                AddressLine2 = string.IsNullOrWhiteSpace(addressLine2) ? null : addressLine2,
                CityId = cityId,
                State = GetOrNull(values, "State"),
                CountryId = countryId,
                PostalCode = string.IsNullOrWhiteSpace(postalCode) ? null : postalCode,
                RemittanceEmail = GetOrNull(values, "RemittanceEmail"),

                PrefixId = prefixId,
                FirstName = GetOrNull(values, "FirstName"),
                AdministrativeContact = GetOrNull(values, "AdministrativeContact"),
                ContactEmail = GetOrNull(values, "ContactEmail"),
                PhoneCountryCodeId = phoneCountryCodeId,
                PhoneAreaCode = GetOrNull(values, "PhoneAreaCode"),
                Phone = string.IsNullOrWhiteSpace(phone) ? null : phone,

                SupplierSiteBuId = supplierSiteBuId,
                ProcurementBuId = procurementBuId,
                ClientBuId = clientBuId,
                BillToBuId = billToBuId,
                CommunicationMethodId = communicationMethodId,
                CommunicationEmail = GetOrNull(values, "CommunicationEmail"),
                IsRfqOrBidding = ParseBool(values, "IsRfqOrBidding"),
                IsOrdering = ParseBool(values, "IsOrdering"),
                IsSourcingOnly = ParseBool(values, "IsSourcingOnly"),
                IsPurchasing = ParseBool(values, "IsPurchasing"),
                IsPay = ParseBool(values, "IsPay"),
                IsPrimaryPay = ParseBool(values, "IsPrimaryPay"),

                PaymentMethodId = paymentMethodId,
                PaymentTermsId = paymentTermsId,
                PaymentMethodCode = GetOrNull(values, "PaymentMethodCode"),
                RemitAdviceEmail = GetOrNull(values, "RemitAdviceEmail"),
                VatCodeId = vatCodeId,
                CurrencyId = currencyId,
                BankId = bankId,
                BankBranchId = bankBranchId,
                AccountCountryCodeId = accountCountryCodeId,
                AccountName = GetOrNull(values, "AccountName"),
                AccountNumber = GetOrNull(values, "AccountNumber"),
                AccountCurrencyCodeId = accountCurrencyCodeId,
                SwiftCode = GetOrNull(values, "SwiftCode"),
                Iban = GetOrNull(values, "Iban"),
                AccountStartDate = ParseDate(values, "AccountStartDate"),
                AllowInternationalPayments = ParseBool(values, "AllowInternationalPayments"),

                BusinessUnitId = businessUnitId,
                CreatedBy = uploadedBy,
                IsDraft = false
            });
        }

        var successCount = suppliers.Count > 0 ? await supplierService.AddRangeAsync(suppliers) : 0;
        return new ImportResult(successCount, errors, warnings);
    }

    private static string? GetOrNull(Dictionary<string, string> values, string field) =>
        values.TryGetValue(field, out var text) && !string.IsNullOrWhiteSpace(text) ? text : null;

    private static bool ParseBool(Dictionary<string, string> values, string field)
    {
        if (!values.TryGetValue(field, out var text) || string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Trim().ToLowerInvariant() is "yes" or "true" or "1" or "y";
    }

    private static DateTime? ParseDate(Dictionary<string, string> values, string field)
    {
        if (!values.TryGetValue(field, out var text) || string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return DateTime.TryParse(text, out var date) ? date : null;
    }

    /// <summary>
    /// Resolves a LOV text value to its LookupValue/BusinessUnit id. A missing required value or an
    /// unmatched required value blocks the row (added to rowErrors). An unmatched value for a field
    /// that isn't required is a soft failure: the field is left blank and a note is added to
    /// rowWarnings instead, so one non-standard optional field (e.g. free-text Payment Terms)
    /// doesn't reject an otherwise valid supplier record.
    /// </summary>
    private static int? ResolveLookup(
        Dictionary<string, string> values, string field, Dictionary<string, int> lookup,
        bool required, List<string> rowErrors, List<string> rowWarnings)
    {
        values.TryGetValue(field, out var text);
        if (string.IsNullOrWhiteSpace(text))
        {
            if (required)
            {
                rowErrors.Add($"{SplitCamelCase(field)} is required.");
            }
            return null;
        }

        if (lookup.TryGetValue(text.ToLowerInvariant(), out var id))
        {
            return id;
        }

        var message = $"Unrecognized {SplitCamelCase(field)} value '{text}' — left blank.";
        if (required)
        {
            rowErrors.Add(message);
        }
        else
        {
            rowWarnings.Add(message);
        }

        return null;
    }

    /// <summary>Resolves Bank/Branch names to ids. Best-effort like the LOV fields: an unrecognized
    /// bank or branch is left blank with a warning rather than rejecting the row.</summary>
    private static (int? BankId, int? BankBranchId) ResolveBankBranch(
        Dictionary<string, string> values, List<Bank> banks, List<string> rowWarnings)
    {
        values.TryGetValue("BankName", out var bankName);
        values.TryGetValue("BranchName", out var branchName);

        if (string.IsNullOrWhiteSpace(bankName))
        {
            if (!string.IsNullOrWhiteSpace(branchName))
            {
                rowWarnings.Add("Branch Name has no matching Bank Name — left blank.");
            }
            return (null, null);
        }

        var bank = banks.FirstOrDefault(b => string.Equals(b.Name, bankName, StringComparison.OrdinalIgnoreCase));
        if (bank is null)
        {
            rowWarnings.Add($"Unrecognized Bank Name value '{bankName}' — left blank.");
            return (null, null);
        }

        if (string.IsNullOrWhiteSpace(branchName))
        {
            return (bank.Id, null);
        }

        var branch = bank.Branches.FirstOrDefault(b => string.Equals(b.Name, branchName, StringComparison.OrdinalIgnoreCase));
        if (branch is null)
        {
            rowWarnings.Add($"Unrecognized Branch Name value '{branchName}' for bank '{bankName}' — left blank.");
            return (bank.Id, null);
        }

        return (bank.Id, branch.Id);
    }

    /// <summary>
    /// Uploaded workbooks may contain multiple sheets (e.g. the official template ships
    /// "Instructions", "Supplier Data Template", "Sheet1", "LOV Details"). Prefer a sheet literally
    /// named "Supplier Data Template"; otherwise pick whichever sheet's header row matches the most
    /// known supplier fields, so a plain single-sheet export still works.
    /// </summary>
    private static IXLWorksheet SelectDataWorksheet(XLWorkbook workbook)
    {
        var named = workbook.Worksheets.FirstOrDefault(w =>
            string.Equals(w.Name.Trim(), "Supplier Data Template", StringComparison.OrdinalIgnoreCase));
        if (named is not null)
        {
            return named;
        }

        return workbook.Worksheets
            .OrderByDescending(w =>
            {
                var headerRow = w.FirstRowUsed();
                if (headerRow is null)
                {
                    return 0;
                }

                return headerRow.CellsUsed().Count(c => HeaderMap.ContainsKey(NormalizeHeader(c.GetString())));
            })
            .First();
    }

    /// <summary>
    /// Strips the decorations used by the Oracle Fusion supplier template (required-field "*",
    /// "(LOV)" suffixes, doubled whitespace) so headers like "*Tax Organization Type (LOV)" match
    /// the plain "tax organization type" keys in <see cref="HeaderMap"/>.
    /// </summary>
    private static string NormalizeHeader(string raw)
    {
        var s = raw.Replace("*", "");
        s = System.Text.RegularExpressions.Regex.Replace(s, "(?i)\\(LOV\\)", "");
        s = System.Text.RegularExpressions.Regex.Replace(s, "\\s+", " ").Trim().ToLowerInvariant();
        return s;
    }

    private static string SplitCamelCase(string value) =>
        System.Text.RegularExpressions.Regex.Replace(value, "(?<!^)([A-Z])", " $1");

    /// <summary>Builds a case-insensitive value→id map keyed by DisplayName. When
    /// <paramref name="includeCode"/> is set (Country, VAT Code, Currency), the short Code is added
    /// as an alias too, so the Oracle template's codes (e.g. "AE", "PBR", "AED") also resolve.
    /// Uses the indexer (not ToDictionary) so a duplicate DisplayName/Code can't throw.</summary>
    private static async Task<Dictionary<string, int>> LoadLookupAsync(
        ApplicationDbContext db, LookupCategory category, bool includeCode = false)
    {
        var rows = await db.LookupValues.Where(l => l.Category == category && l.IsActive).ToListAsync();
        var map = new Dictionary<string, int>();
        foreach (var l in rows)
        {
            map[l.DisplayName.ToLowerInvariant()] = l.Id;
            if (includeCode && !string.IsNullOrWhiteSpace(l.Code))
            {
                map.TryAdd(l.Code.ToLowerInvariant(), l.Id);
            }
        }
        return map;
    }

    private static async Task<Dictionary<string, int>> LoadCountriesByPhoneCodeAsync(ApplicationDbContext db)
    {
        var rows = await db.LookupValues
            .Where(l => l.Category == LookupCategory.Country && l.IsActive && l.PhoneCode != null)
            .ToListAsync();
        return rows
            .GroupBy(l => l.PhoneCode!.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First().Id);
    }
}
