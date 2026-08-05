using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data;
using SupplierPortal.Web.Models;

namespace SupplierPortal.Web.Services;

public record ImportRowError(int RowNumber, string Message);

public record ImportResult(int SuccessCount, List<ImportRowError> Errors, List<ImportRowError> Warnings);

public class ExcelImportService(
    IDbContextFactory<ApplicationDbContext> dbFactory, SupplierService supplierService, SupplierFieldSettingService fieldSettingService)
{
    /// <summary>Maps an ExcelImportService-internal field name to the <see cref="SupplierFieldSetting"/>
    /// key (matching <see cref="SupplierFormModel"/> property names) that governs whether it's
    /// hard-required for import, mirroring the same admin-configured setting used by the
    /// registration wizard. Fields with no entry here (e.g. Currency, a legacy field not on the
    /// wizard) are never hard-required on import.</summary>
    private static readonly Dictionary<string, string> ImportFieldToSettingKey = new()
    {
        ["SupplierName"] = "SupplierName",
        ["TaxRegistrationNumber"] = "TaxRegistrationNumber",
        ["TaxOrganizationType"] = "TaxOrganizationTypeId",
        ["SupplierType"] = "SupplierTypeId",
        ["TaxpayerCountry"] = "TaxpayerCountryId",
        ["CountryName"] = "CountryNameId",
        ["Country"] = "CountryId",
        ["BusinessRelationship"] = "BusinessRelationshipId",
        ["City"] = "CityId",
        ["Province"] = "ProvinceId",
        ["VatCode"] = "VatCodeId",
        ["PaymentMethod"] = "PaymentMethodId",
        ["PaymentTerms"] = "PaymentTermsId",
        ["Prefix"] = "PrefixId",
        ["CommunicationMethod"] = "CommunicationMethodId",
        ["AccountCountryCode"] = "AccountCountryCodeId",
        ["AccountCurrencyCode"] = "AccountCurrencyCodeId",
        ["PhoneCountryCode"] = "PhoneCountryCodeId",
        ["SupplierSite"] = "SupplierSiteBuId",
        ["ProcurementBu"] = "ProcurementBuId",
        ["ClientBu"] = "ClientBuId",
        ["BillToBu"] = "BillToBuId",
        ["BankName"] = "BankId",
        ["BranchName"] = "BankBranchId",
        ["TradeLicenseNumber"] = "TradeLicenseNumber",
        ["DunsNumber"] = "DunsNumber",
        ["CorporateWebSite"] = "CorporateWebSite",
        ["AddressName"] = "AddressName",
        ["AddressLine1"] = "AddressLine1",
        ["AddressLine2"] = "AddressLine2",
        ["State"] = "State",
        ["PostalCode"] = "PostalCode",
        ["RemittanceEmail"] = "RemittanceEmail",
        ["FirstName"] = "FirstName",
        ["AdministrativeContact"] = "AdministrativeContact",
        ["PhoneAreaCode"] = "PhoneAreaCode",
        ["Phone"] = "Phone",
        ["PaymentMethodCode"] = "PaymentMethodCode",
        ["RemitAdviceEmail"] = "RemitAdviceEmail",
        ["AccountName"] = "AccountName",
        ["AccountNumber"] = "AccountNumber",
        ["SwiftCode"] = "SwiftCode",
        ["Iban"] = "Iban",
        ["AccountStartDate"] = "AccountStartDate",
        ["ContactEmail"] = "ContactEmail",
        ["CommunicationEmail"] = "CommunicationEmail"
    };

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
        ["province"] = "Province",
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

        var mandatorySettingKeys = await fieldSettingService.GetMandatoryFieldKeysAsync();
        bool IsMandatory(string importField) =>
            ImportFieldToSettingKey.TryGetValue(importField, out var settingKey) && mandatorySettingKeys.Contains(settingKey);

        var taxOrgTypes = await LoadLookupAsync(db, LookupCategory.TaxOrganizationType);
        var supplierTypes = await LoadLookupAsync(db, LookupCategory.SupplierType);
        var countries = await LoadLookupAsync(db, LookupCategory.Country);
        var countriesByPhoneCode = await LoadCountriesByPhoneCodeAsync(db);
        var businessRelationships = await LoadLookupAsync(db, LookupCategory.BusinessRelationship);
        var provinces = await LoadLookupAsync(db, LookupCategory.Province);
        var vatCodes = await LoadLookupAsync(db, LookupCategory.VatCode);
        var paymentMethods = await LoadLookupAsync(db, LookupCategory.PaymentMethod);
        var paymentTerms = await LoadLookupAsync(db, LookupCategory.PaymentTerms);
        var prefixes = await LoadLookupAsync(db, LookupCategory.Prefix);
        var communicationMethods = await LoadLookupAsync(db, LookupCategory.CommunicationMethod);
        var currencies = await LoadLookupAsync(db, LookupCategory.Currency);
        var banks = await db.Banks.Include(b => b.Branches).ToListAsync();
        var businessUnits = await db.BusinessUnits.ToDictionaryAsync(b => b.Name.ToLowerInvariant(), b => b.Id);

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
            if (IsMandatory("TaxRegistrationNumber") && string.IsNullOrWhiteSpace(taxRegistrationNumber)) rowErrors.Add("Tax Registration Number is required.");

            var taxOrgTypeId = ResolveLookup(values, "TaxOrganizationType", taxOrgTypes, IsMandatory("TaxOrganizationType"), rowErrors, rowWarnings);
            var supplierTypeId = ResolveLookup(values, "SupplierType", supplierTypes, IsMandatory("SupplierType"), rowErrors, rowWarnings);
            var taxpayerCountryId = ResolveLookup(values, "TaxpayerCountry", countries, IsMandatory("TaxpayerCountry"), rowErrors, rowWarnings);
            var countryNameId = ResolveLookup(values, "CountryName", countries, IsMandatory("CountryName"), rowErrors, rowWarnings);
            var countryId = ResolveLookup(values, "Country", countries, IsMandatory("Country"), rowErrors, rowWarnings);
            var businessRelationshipId = ResolveLookup(values, "BusinessRelationship", businessRelationships, IsMandatory("BusinessRelationship"), rowErrors, rowWarnings);
            var cityId = ResolveLookup(values, "City", provinces, IsMandatory("City"), rowErrors, rowWarnings);
            var provinceId = ResolveLookup(values, "Province", provinces, IsMandatory("Province"), rowErrors, rowWarnings);
            var vatCodeId = ResolveLookup(values, "VatCode", vatCodes, IsMandatory("VatCode"), rowErrors, rowWarnings);
            var paymentMethodId = ResolveLookup(values, "PaymentMethod", paymentMethods, IsMandatory("PaymentMethod"), rowErrors, rowWarnings);
            var paymentTermsId = ResolveLookup(values, "PaymentTerms", paymentTerms, IsMandatory("PaymentTerms"), rowErrors, rowWarnings);
            var prefixId = ResolveLookup(values, "Prefix", prefixes, IsMandatory("Prefix"), rowErrors, rowWarnings);
            var communicationMethodId = ResolveLookup(values, "CommunicationMethod", communicationMethods, IsMandatory("CommunicationMethod"), rowErrors, rowWarnings);
            var currencyId = ResolveLookup(values, "Currency", currencies, required: false, rowErrors, rowWarnings);
            var accountCountryCodeId = ResolveLookup(values, "AccountCountryCode", countries, IsMandatory("AccountCountryCode"), rowErrors, rowWarnings);
            var accountCurrencyCodeId = ResolveLookup(values, "AccountCurrencyCode", currencies, IsMandatory("AccountCurrencyCode"), rowErrors, rowWarnings);
            var phoneCountryCodeId = ResolveLookup(values, "PhoneCountryCode", countriesByPhoneCode, IsMandatory("PhoneCountryCode"), rowErrors, rowWarnings);
            var supplierSiteBuId = ResolveLookup(values, "SupplierSite", businessUnits, IsMandatory("SupplierSite"), rowErrors, rowWarnings);
            var procurementBuId = ResolveLookup(values, "ProcurementBu", businessUnits, IsMandatory("ProcurementBu"), rowErrors, rowWarnings);
            var clientBuId = ResolveLookup(values, "ClientBu", businessUnits, IsMandatory("ClientBu"), rowErrors, rowWarnings);
            var billToBuId = ResolveLookup(values, "BillToBu", businessUnits, IsMandatory("BillToBu"), rowErrors, rowWarnings);
            var (bankId, bankBranchId) = ResolveBankBranch(values, banks, IsMandatory("BankName"), IsMandatory("BranchName"), rowErrors);

            values.TryGetValue("AddressLine2", out var addressLine2);
            values.TryGetValue("PostalCode", out var postalCode);
            values.TryGetValue("Phone", out var phone);

            RequireIfMandatory("TradeLicenseNumber", rowErrors, IsMandatory, values);
            RequireIfMandatory("DunsNumber", rowErrors, IsMandatory, values);
            RequireIfMandatory("CorporateWebSite", rowErrors, IsMandatory, values);
            RequireIfMandatory("AddressName", rowErrors, IsMandatory, values);
            RequireIfMandatory("AddressLine1", rowErrors, IsMandatory, values);
            RequireIfMandatory("AddressLine2", rowErrors, IsMandatory, values);
            RequireIfMandatory("State", rowErrors, IsMandatory, values);
            RequireIfMandatory("PostalCode", rowErrors, IsMandatory, values);
            RequireIfMandatory("RemittanceEmail", rowErrors, IsMandatory, values);
            RequireIfMandatory("FirstName", rowErrors, IsMandatory, values);
            RequireIfMandatory("AdministrativeContact", rowErrors, IsMandatory, values);
            RequireIfMandatory("PhoneAreaCode", rowErrors, IsMandatory, values);
            RequireIfMandatory("Phone", rowErrors, IsMandatory, values);
            RequireIfMandatory("PaymentMethodCode", rowErrors, IsMandatory, values);
            RequireIfMandatory("RemitAdviceEmail", rowErrors, IsMandatory, values);
            RequireIfMandatory("AccountName", rowErrors, IsMandatory, values);
            RequireIfMandatory("AccountNumber", rowErrors, IsMandatory, values);
            RequireIfMandatory("SwiftCode", rowErrors, IsMandatory, values);
            RequireIfMandatory("Iban", rowErrors, IsMandatory, values);
            RequireIfMandatory("AccountStartDate", rowErrors, IsMandatory, values);
            RequireIfMandatory("ContactEmail", rowErrors, IsMandatory, values);
            RequireIfMandatory("CommunicationEmail", rowErrors, IsMandatory, values);

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
                TaxRegistrationNumber = taxRegistrationNumber!,
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
                ProvinceId = provinceId,
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

    private static void RequireIfMandatory(
        string field, List<string> rowErrors, Func<string, bool> isMandatory, Dictionary<string, string> values)
    {
        if (!isMandatory(field)) return;
        if (!values.TryGetValue(field, out var text) || string.IsNullOrWhiteSpace(text))
        {
            rowErrors.Add($"{SplitCamelCase(field)} is required.");
        }
    }

    private static (int? BankId, int? BankBranchId) ResolveBankBranch(
        Dictionary<string, string> values, List<Bank> banks, bool bankRequired, bool branchRequired, List<string> rowErrors)
    {
        values.TryGetValue("BankName", out var bankName);
        values.TryGetValue("BranchName", out var branchName);

        if (string.IsNullOrWhiteSpace(bankName))
        {
            if (!string.IsNullOrWhiteSpace(branchName))
            {
                rowErrors.Add("Branch Name requires a Bank Name.");
            }
            else if (bankRequired)
            {
                rowErrors.Add("Bank Name is required.");
            }
            return (null, null);
        }

        var bank = banks.FirstOrDefault(b => string.Equals(b.Name, bankName, StringComparison.OrdinalIgnoreCase));
        if (bank is null)
        {
            rowErrors.Add($"Unrecognized Bank Name value '{bankName}'.");
            return (null, null);
        }

        if (string.IsNullOrWhiteSpace(branchName))
        {
            if (branchRequired)
            {
                rowErrors.Add("Branch Name is required.");
            }
            return (bank.Id, null);
        }

        var branch = bank.Branches.FirstOrDefault(b => string.Equals(b.Name, branchName, StringComparison.OrdinalIgnoreCase));
        if (branch is null)
        {
            rowErrors.Add($"Unrecognized Branch Name value '{branchName}' for bank '{bankName}'.");
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

    private static async Task<Dictionary<string, int>> LoadLookupAsync(ApplicationDbContext db, LookupCategory category)
    {
        var rows = await db.LookupValues.Where(l => l.Category == category && l.IsActive).ToListAsync();
        return rows.ToDictionary(l => l.DisplayName.ToLowerInvariant(), l => l.Id);
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
