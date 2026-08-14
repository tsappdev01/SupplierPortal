using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Models;
using SupplierPortal.Web.Services;

namespace SupplierPortal.Web.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db, IConfiguration configuration)
    {
        await db.Database.MigrateAsync();

        await SeedLookupsAsync(db);
        await SeedBanksAsync(db);
        await SeedBusinessUnitsAsync(db);
        await SeedSupplierFieldSettingsAsync(db);
        await SeedDefaultAdminAsync(db, configuration);
    }

    private static async Task SeedLookupsAsync(ApplicationDbContext db)
    {
        // Reconciled (not skip-if-exists) so databases seeded earlier with a different Supplier Type /
        // Tax Organization Type list are brought into line with the canonical lists below.
        await ReconcileSimpleCategoryAsync(db, LookupCategory.TaxOrganizationType, SupplierMasterSeedData.TaxOrganizationTypes);
        await ReconcileSimpleCategoryAsync(db, LookupCategory.SupplierType, SupplierMasterSeedData.SupplierTypes);
        await SeedSimpleCategoryAsync(db, LookupCategory.Province, SupplierMasterSeedData.Provinces);
        await SeedSimpleCategoryAsync(db, LookupCategory.PaymentMethod, SupplierMasterSeedData.PaymentMethods);
        await SeedSimpleCategoryAsync(db, LookupCategory.PaymentTerms, SupplierMasterSeedData.PaymentTerms);
        await SeedSimpleCategoryAsync(db, LookupCategory.Prefix, SupplierMasterSeedData.Prefixes);
        await SeedSimpleCategoryAsync(db, LookupCategory.BusinessRelationship, SupplierMasterSeedData.BusinessRelationships);
        await SeedSimpleCategoryAsync(db, LookupCategory.CommunicationMethod, SupplierMasterSeedData.CommunicationMethods);
        await SeedSimpleCategoryAsync(db, LookupCategory.SupplierStatus, SupplierMasterSeedData.SupplierStatuses);

        // Idempotent: bring earlier workflow-status labels up to their current display names on
        // databases that were seeded before the rename (New -> Not Submitted, Complete/Completes -> Completed).
        await RenameLookupAsync(db, LookupCategory.SupplierStatus, "New", SupplierStatuses.New);
        await RenameLookupAsync(db, LookupCategory.SupplierStatus, "Complete", SupplierStatuses.Complete);
        await RenameLookupAsync(db, LookupCategory.SupplierStatus, "Completes", SupplierStatuses.Complete);
        await SeedPairCategoryAsync(db, LookupCategory.VatCode, SupplierMasterSeedData.VatCodes);
        await SeedPairCategoryAsync(db, LookupCategory.Currency, SupplierMasterSeedData.Currencies);

        // LOVs added for the extended Supplier master fields (VAT structure, partner/location type,
        // eInvoicing wave, licensing authority). Seeded per-category so they appear on existing DBs.
        await SeedSimpleCategoryAsync(db, LookupCategory.VatRegistrationType, SupplierMasterSeedData.VatRegistrationTypes);
        await SeedPairCategoryAsync(db, LookupCategory.BusinessPartnerType, SupplierMasterSeedData.BusinessPartnerTypes);
        await SeedSimpleCategoryAsync(db, LookupCategory.BusinessLocationType, SupplierMasterSeedData.BusinessLocationTypes);
        await SeedSimpleCategoryAsync(db, LookupCategory.EInvoicingWave, SupplierMasterSeedData.EInvoicingWaves);
        await SeedPairCategoryAsync(db, LookupCategory.LicensingAuthority, SupplierMasterSeedData.LicensingAuthorities);

        if (!await db.LookupValues.AnyAsync(l => l.Category == LookupCategory.Country))
        {
            db.LookupValues.AddRange(CountrySeedData.Countries.Select(c => new LookupValue
            {
                Category = LookupCategory.Country,
                Code = c.Code,
                DisplayName = c.Name,
                PhoneCode = c.PhoneCode
            }));
            await db.SaveChangesAsync();
        }
        else
        {
            // Backfill phone dialing codes onto existing Country lookups. Databases seeded before
            // PhoneCode was populated have null codes, which leaves the phone/mobile country selector
            // empty. Match by ISO code first, then by name, and only fill rows that are missing one.
            var countryRows = await db.LookupValues
                .Where(l => l.Category == LookupCategory.Country && (l.PhoneCode == null || l.PhoneCode == ""))
                .ToListAsync();
            if (countryRows.Count > 0)
            {
                var byCode = CountrySeedData.Countries
                    .Where(c => !string.IsNullOrWhiteSpace(c.PhoneCode))
                    .GroupBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().PhoneCode!, StringComparer.OrdinalIgnoreCase);
                var byName = CountrySeedData.Countries
                    .Where(c => !string.IsNullOrWhiteSpace(c.PhoneCode))
                    .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().PhoneCode!, StringComparer.OrdinalIgnoreCase);

                var changed = false;
                foreach (var row in countryRows)
                {
                    if ((byCode.TryGetValue(row.Code, out var code) && !string.IsNullOrWhiteSpace(code))
                        || (byName.TryGetValue(row.DisplayName, out code) && !string.IsNullOrWhiteSpace(code)))
                    {
                        row.PhoneCode = code;
                        changed = true;
                    }
                }
                if (changed)
                {
                    await db.SaveChangesAsync();
                }
            }
        }
    }

    private static async Task RenameLookupAsync(ApplicationDbContext db, LookupCategory category, string oldName, string newName)
    {
        if (oldName == newName)
        {
            return;
        }

        var rows = await db.LookupValues.Where(l => l.Category == category && l.DisplayName == oldName).ToListAsync();
        foreach (var row in rows)
        {
            row.DisplayName = newName;
        }
        if (rows.Count > 0)
        {
            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Brings a simple (DisplayName == Code) lookup category into line with the supplied canonical list,
    /// unlike <see cref="SeedSimpleCategoryAsync"/> which only seeds when the category is empty. Missing
    /// values are inserted (or reactivated); values no longer in the canonical list are deactivated rather
    /// than deleted so existing Supplier foreign-key references (DeleteBehavior.Restrict) stay intact.
    /// </summary>
    private static async Task ReconcileSimpleCategoryAsync(ApplicationDbContext db, LookupCategory category, string[] values)
    {
        var existing = await db.LookupValues.Where(l => l.Category == category).ToListAsync();
        var desired = values.Select(v => v.Trim()).ToArray();
        var desiredSet = new HashSet<string>(desired, StringComparer.OrdinalIgnoreCase);

        var changed = false;

        // Insert missing values; reactivate any that exist but were previously deactivated.
        foreach (var value in desired)
        {
            var match = existing.FirstOrDefault(e => string.Equals(e.DisplayName, value, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                db.LookupValues.Add(new LookupValue
                {
                    Category = category,
                    Code = value,
                    DisplayName = value,
                    IsActive = true
                });
                changed = true;
            }
            else if (!match.IsActive)
            {
                match.IsActive = true;
                changed = true;
            }
        }

        // Deactivate anything not in the canonical list so it disappears from dropdowns without
        // breaking historical references.
        foreach (var row in existing)
        {
            if (!desiredSet.Contains(row.DisplayName) && row.IsActive)
            {
                row.IsActive = false;
                changed = true;
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync();
        }
    }

    private static async Task SeedSimpleCategoryAsync(ApplicationDbContext db, LookupCategory category, string[] values)
    {
        if (await db.LookupValues.AnyAsync(l => l.Category == category))
        {
            return;
        }

        db.LookupValues.AddRange(values.Select(v => new LookupValue
        {
            Category = category,
            Code = v.Trim(),
            DisplayName = v.Trim()
        }));
        await db.SaveChangesAsync();
    }

    private static async Task SeedPairCategoryAsync(ApplicationDbContext db, LookupCategory category, (string Code, string DisplayName)[] pairs)
    {
        if (await db.LookupValues.AnyAsync(l => l.Category == category))
        {
            return;
        }

        db.LookupValues.AddRange(pairs.Select(p => new LookupValue
        {
            Category = category,
            Code = p.Code.Trim(),
            DisplayName = p.DisplayName.Trim()
        }));
        await db.SaveChangesAsync();
    }

    private static async Task SeedBanksAsync(ApplicationDbContext db)
    {
        if (await db.Banks.AnyAsync())
        {
            return;
        }

        foreach (var (bankName, branches) in BankSeedData.Banks)
        {
            var bank = new Bank { Name = bankName.Trim() };
            bank.Branches.AddRange(branches.Select(b => new BankBranch { Name = b.Trim() }));
            db.Banks.Add(bank);
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedBusinessUnitsAsync(ApplicationDbContext db)
    {
        if (await db.BusinessUnits.AnyAsync())
        {
            return;
        }

        foreach (var name in BusinessUnitSeedData.Names)
        {
            var trimmedName = name.Trim();
            var baseCode = BusinessUnitService.GenerateBaseCode(trimmedName);
            var code = await BusinessUnitService.ResolveUniqueCodeAsync(db, baseCode);

            db.BusinessUnits.Add(new BusinessUnit { Name = trimmedName, Code = code });
            await db.SaveChangesAsync();
        }
    }

    private static async Task SeedSupplierFieldSettingsAsync(ApplicationDbContext db)
    {
        // Add any seed field that isn't already present, so fields introduced after the first run
        // (e.g. the site-usage checkboxes) appear in Supplier Field Settings without wiping the
        // administrator's existing mandatory/visibility choices for the fields already there.
        var existing = await db.SupplierFieldSettings.ToListAsync();
        var existingByKey = existing.ToDictionary(f => f.FieldKey);
        var nextSequence = existing.Count > 0 ? existing.Max(f => f.Sequence) + 1 : 0;

        var changed = false;
        foreach (var f in SupplierFieldSettingSeedData.Fields)
        {
            if (existingByKey.TryGetValue(f.FieldKey, out var row))
            {
                // Keep the administrator's Mandatory / Show-to-Supplier choices, but bring the
                // display label and wizard step into line with the current spec (so relabels and
                // step re-groupings ship without wiping configuration).
                if (row.DisplayLabel != f.DisplayLabel || row.Step != f.Step)
                {
                    row.DisplayLabel = f.DisplayLabel;
                    row.Step = f.Step;
                    changed = true;
                }
            }
            else
            {
                db.SupplierFieldSettings.Add(new SupplierFieldSetting
                {
                    FieldKey = f.FieldKey,
                    DisplayLabel = f.DisplayLabel,
                    Step = f.Step,
                    Sequence = nextSequence++,
                    IsMandatory = f.IsMandatory,
                    IsVisibleToSupplier = true
                });
                changed = true;
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync();
        }
    }

    private static async Task SeedDefaultAdminAsync(ApplicationDbContext db, IConfiguration configuration)
    {
        var anyRealAdmin = await db.Users.AnyAsync(u => u.UserType == UserType.Administrator && !u.IsDefaultAdmin);
        if (anyRealAdmin)
        {
            return;
        }

        var defaultAdminExists = await db.Users.AnyAsync(u => u.IsDefaultAdmin);
        if (defaultAdminExists)
        {
            return;
        }

        var username = configuration["DefaultAdmin:Username"] ?? "admin";
        var email = configuration["DefaultAdmin:Email"] ?? "admin@supplierportal.local";
        var password = configuration["DefaultAdmin:Password"] ?? "Admin@12345";

        var hasher = new PasswordHasher<User>();
        var admin = new User
        {
            Username = username,
            FullName = "System Administrator",
            Email = email,
            UserType = UserType.Administrator,
            IsActive = true,
            IsDefaultAdmin = true,
            IsPendingRegistration = false
        };
        admin.PasswordHash = hasher.HashPassword(admin, password);

        db.Users.Add(admin);
        await db.SaveChangesAsync();
    }
}
