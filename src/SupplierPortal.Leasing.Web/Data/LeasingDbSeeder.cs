using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data.Leasing;
using SupplierPortal.Web.Models;

namespace SupplierPortal.Leasing.Web.Data;

/// <summary>Minimal startup seed for the standalone Landlord database: ensures a default Business Unit and a
/// break-glass administrator (with Landlord portal access) exist so the app is usable on a fresh database.
/// Landlord field configuration seeds itself on first use via LandlordFieldConfigService.</summary>
public static class LeasingDbSeeder
{
    public static async Task SeedAsync(LeasingDbContext db, IConfiguration configuration)
    {
        // Default business unit.
        if (!await db.BusinessUnits.AnyAsync())
        {
            db.BusinessUnits.Add(new BusinessUnit
            {
                Code = configuration["DefaultBusinessUnit:Code"] ?? "DIP",
                Name = configuration["DefaultBusinessUnit:Name"] ?? "Dubai Investments Park",
            });
            await db.SaveChangesAsync();
        }

        // Break-glass administrator (only when there is no real administrator yet).
        var anyRealAdmin = await db.Users.AnyAsync(u => u.UserType == UserType.Administrator && !u.IsDefaultAdmin);
        if (!anyRealAdmin && !await db.Users.AnyAsync(u => u.IsDefaultAdmin))
        {
            var admin = new User
            {
                Username = configuration["DefaultAdmin:Username"] ?? "admin",
                FullName = "System Administrator",
                Email = configuration["DefaultAdmin:Email"] ?? "admin@landlordportal.local",
                UserType = UserType.Administrator,
                PortalAccess = PortalAccess.Landlord,
                IsActive = true,
                IsDefaultAdmin = true,
                IsPendingRegistration = false
            };
            admin.PasswordHash = new PasswordHasher<User>()
                .HashPassword(admin, configuration["DefaultAdmin:Password"] ?? "Admin@12345");
            db.Users.Add(admin);
            await db.SaveChangesAsync();
        }
    }
}
