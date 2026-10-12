using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Data;
using SupplierPortal.Web.Models;
using SupplierPortal.Web.Models.Leasing;

namespace SupplierPortal.Web.Data.Leasing;

/// <summary>
/// The Landlord application's database context. As the standalone Landlord app runs against its OWN separate
/// database, this context owns BOTH the shared framework tables (identity, business units, departments,
/// lookups, audit — shaped by <see cref="SharedModelConfig"/>) AND the <c>leasing.*</c> tables, and reads the
/// read-only land view <c>dbo.Property</c> (<see cref="Premises"/>). Implementing <see cref="IAppDbContext"/>
/// lets every shared service in SupplierPortal.Shared run against it unchanged.
/// </summary>
public class LeasingDbContext(DbContextOptions<LeasingDbContext> options) : DbContext(options), IAppDbContext
{
    public const string Schema = "leasing";

    // ---- Shared framework tables (shaped by SharedModelConfig) ----
    public DbSet<User> Users => Set<User>();
    public DbSet<BusinessUnit> BusinessUnits => Set<BusinessUnit>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<LookupValue> LookupValues => Set<LookupValue>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // ---- Landlord (leasing.*) tables ----
    public DbSet<Premises> Premises => Set<Premises>();
    public DbSet<Landlord> Landlords => Set<Landlord>();
    public DbSet<LandlordPremises> LandlordPremises => Set<LandlordPremises>();
    public DbSet<LandlordContact> LandlordContacts => Set<LandlordContact>();
    public DbSet<LandlordSignatory> LandlordSignatories => Set<LandlordSignatory>();
    public DbSet<LandlordDocument> LandlordDocuments => Set<LandlordDocument>();
    public DbSet<LandlordRegistrationComment> LandlordComments => Set<LandlordRegistrationComment>();
    public DbSet<LandlordFieldChange> LandlordFieldChanges => Set<LandlordFieldChange>();
    public DbSet<LandlordEvent> LandlordEvents => Set<LandlordEvent>();
    public DbSet<LandlordAccessToken> LandlordAccessTokens => Set<LandlordAccessToken>();
    public DbSet<MaintainTenantUaePassEmail> MaintainTenantUaePassEmails => Set<MaintainTenantUaePassEmail>();
    public DbSet<LandlordFieldConfig> LandlordFieldConfigs => Set<LandlordFieldConfig>();
    public DbSet<LandlordTabSetting> LandlordTabSettings => Set<LandlordTabSetting>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // Shared framework tables live in this same database (the Landlord app is self-contained).
        SharedModelConfig.Apply(b);

        // Read-only land view — never migrated.
        b.Entity<Premises>(e =>
        {
            e.ToView("Property");            // [dbo].[Property]
            e.HasKey(p => p.Id);
            e.Property(p => p.Id).ValueGeneratedNever();
        });

        // Existing shared-DB table (dbo), created by the business's DDL — mapped for writing, excluded from
        // our migrations. Populated on approval with the registration's signatory emails.
        b.Entity<MaintainTenantUaePassEmail>(e =>
        {
            e.ToTable("MaintainTenantUAEPassEmails", "dbo", t => t.ExcludeFromMigrations());
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).HasColumnType("varchar(255)");
            e.Property(x => x.AndOr).HasColumnName("AND_OR").HasColumnType("char(3)");
        });

        b.Entity<Landlord>(e =>
        {
            e.ToTable("Landlords", Schema);
            e.HasIndex(x => x.BusinessUnitId);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.LegacySourceId);
            e.HasMany(x => x.Premises).WithOne(x => x.Landlord!).HasForeignKey(x => x.LandlordId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Contacts).WithOne(x => x.Landlord!).HasForeignKey(x => x.LandlordId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Signatories).WithOne(x => x.Landlord!).HasForeignKey(x => x.LandlordId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Documents).WithOne(x => x.Landlord!).HasForeignKey(x => x.LandlordId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Comments).WithOne(x => x.Landlord!).HasForeignKey(x => x.LandlordId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<LandlordContact>(e =>
        {
            e.ToTable("LandlordContacts", Schema);
            e.HasIndex(x => x.LandlordId);
        });

        b.Entity<LandlordRegistrationComment>(e =>
        {
            e.ToTable("LandlordComments", Schema);
            e.HasIndex(x => new { x.LandlordId, x.IsResolved });
        });

        b.Entity<LandlordPremises>(e =>
        {
            e.ToTable("LandlordPremises", Schema);
            e.HasIndex(x => new { x.LandlordId, x.PremisesId }).IsUnique();
            e.HasIndex(x => x.PremisesId);
        });

        b.Entity<LandlordSignatory>(e =>
        {
            e.ToTable("LandlordSignatories", Schema);
            e.HasIndex(x => x.LandlordId);
        });

        b.Entity<LandlordDocument>(e =>
        {
            e.ToTable("LandlordDocuments", Schema);
            e.HasIndex(x => x.LandlordId);
            // Base64 content stored in-row as varchar(max) (ASCII) rather than on the file system.
            e.Property(x => x.ContentBase64).HasColumnType("varchar(max)");
        });

        b.Entity<LandlordFieldChange>(e =>
        {
            e.ToTable("LandlordFieldChanges", Schema);
            e.HasIndex(x => new { x.LandlordId, x.IsAcknowledged });
        });

        b.Entity<LandlordEvent>(e =>
        {
            e.ToTable("LandlordEvents", Schema);
            e.HasIndex(x => x.LandlordId);
        });

        b.Entity<LandlordAccessToken>(e =>
        {
            e.ToTable("LandlordAccessTokens", Schema);
            e.HasIndex(x => x.TokenHash);
            e.HasOne(x => x.Landlord!).WithMany().HasForeignKey(x => x.LandlordId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<LandlordFieldConfig>(e =>
        {
            e.ToTable("LandlordFieldConfigs", Schema);
            e.HasIndex(x => new { x.Section, x.FieldKey }).IsUnique();
        });

        b.Entity<LandlordTabSetting>(e =>
        {
            e.ToTable("LandlordTabSettings", Schema);
            e.HasIndex(x => new { x.Section, x.GroupName }).IsUnique();
        });
    }
}
