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
        await SeedSimpleCategoryAsync(db, LookupCategory.TaxOrganizationType, SupplierMasterSeedData.TaxOrganizationTypes);
        await SeedSimpleCategoryAsync(db, LookupCategory.SupplierType, SupplierMasterSeedData.SupplierTypes);
        await SeedSimpleCategoryAsync(db, LookupCategory.Province, SupplierMasterSeedData.Provinces);
        await SeedSimpleCategoryAsync(db, LookupCategory.PaymentMethod, SupplierMasterSeedData.PaymentMethods);
        await SeedSimpleCategoryAsync(db, LookupCategory.PaymentTerms, SupplierMasterSeedData.PaymentTerms);
        await SeedSimpleCategoryAsync(db, LookupCategory.Prefix, SupplierMasterSeedData.Prefixes);
        await SeedSimpleCategoryAsync(db, LookupCategory.BusinessRelationship, SupplierMasterSeedData.BusinessRelationships);
        await SeedSimpleCategoryAsync(db, LookupCategory.CommunicationMethod, SupplierMasterSeedData.CommunicationMethods);
        await SeedSimpleCategoryAsync(db, LookupCategory.SupplierStatus, SupplierMasterSeedData.SupplierStatuses);
        await SeedPairCategoryAsync(db, LookupCategory.VatCode, SupplierMasterSeedData.VatCodes);
        await SeedPairCategoryAsync(db, LookupCategory.Currency, SupplierMasterSeedData.Currencies);

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
        if (await db.SupplierFieldSettings.AnyAsync())
        {
            return;
        }

        var sequence = 0;
        db.SupplierFieldSettings.AddRange(SupplierFieldSettingSeedData.Fields.Select(f => new SupplierFieldSetting
        {
            FieldKey = f.FieldKey,
            DisplayLabel = f.DisplayLabel,
            Step = f.Step,
            Sequence = sequence++,
            IsMandatory = f.IsMandatory
        }));
        await db.SaveChangesAsync();
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
