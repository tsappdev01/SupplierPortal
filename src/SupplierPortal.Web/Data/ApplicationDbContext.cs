using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Models;
using SupplierPortal.Web.Models.Customer;

namespace SupplierPortal.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<BusinessUnit> BusinessUnits => Set<BusinessUnit>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<LookupValue> LookupValues => Set<LookupValue>();
    public DbSet<Bank> Banks => Set<Bank>();
    public DbSet<BankBranch> BankBranches => Set<BankBranch>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SupplierFieldSetting> SupplierFieldSettings => Set<SupplierFieldSetting>();
    public DbSet<SupplierAccessToken> SupplierAccessTokens => Set<SupplierAccessToken>();
    public DbSet<SupplierFieldChange> SupplierFieldChanges => Set<SupplierFieldChange>();
    public DbSet<SupplierDocument> SupplierDocuments => Set<SupplierDocument>();
    public DbSet<SupplierPartner> SupplierPartners => Set<SupplierPartner>();

    // ---- CST (Customer) Portal ----
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerContact> CustomerContacts => Set<CustomerContact>();
    public DbSet<CustomerDocument> CustomerDocuments => Set<CustomerDocument>();
    public DbSet<CustomerFieldConfig> CustomerFieldConfigs => Set<CustomerFieldConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ---- CST (Customer) Portal relationships ----
        modelBuilder.Entity<Customer>()
            .HasOne(c => c.BusinessUnit)
            .WithMany()
            .HasForeignKey(c => c.BusinessUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Customer>()
            .HasOne(c => c.Contact)
            .WithOne(c => c.Customer!)
            .HasForeignKey<CustomerContact>(c => c.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Customer>()
            .HasMany(c => c.Documents)
            .WithOne(d => d.Customer!)
            .HasForeignKey(d => d.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasMany(u => u.BusinessUnits)
            .WithMany(b => b.Users)
            .UsingEntity<Dictionary<string, object>>(
                "UserBusinessUnit",
                j => j.HasOne<BusinessUnit>().WithMany().HasForeignKey("BusinessUnitId").OnDelete(DeleteBehavior.Cascade),
                j => j.HasOne<User>().WithMany().HasForeignKey("UserId").OnDelete(DeleteBehavior.Cascade),
                j =>
                {
                    j.HasKey("UserId", "BusinessUnitId");
                    j.ToTable("UserBusinessUnits");
                });

        modelBuilder.Entity<User>()
            .HasOne(u => u.CompanyBusinessUnit)
            .WithMany()
            .HasForeignKey(u => u.CompanyBusinessUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<BusinessUnit>()
            .HasIndex(b => b.Code)
            .IsUnique();

        modelBuilder.Entity<Supplier>()
            .HasOne(s => s.BusinessUnit)
            .WithMany(b => b.Suppliers)
            .HasForeignKey(s => s.BusinessUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Supplier>()
            .HasOne(s => s.ProcurementBu)
            .WithMany()
            .HasForeignKey(s => s.ProcurementBuId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Supplier>()
            .HasOne(s => s.ClientBu)
            .WithMany()
            .HasForeignKey(s => s.ClientBuId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Supplier>()
            .HasOne(s => s.BillToBu)
            .WithMany()
            .HasForeignKey(s => s.BillToBuId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Supplier>()
            .HasOne(s => s.SupplierSiteBu)
            .WithMany()
            .HasForeignKey(s => s.SupplierSiteBuId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Supplier>()
            .HasOne(s => s.City)
            .WithMany()
            .HasForeignKey(s => s.CityId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Supplier>()
            .HasOne(s => s.TaxOrganizationType)
            .WithMany()
            .HasForeignKey(s => s.TaxOrganizationTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Supplier>()
            .HasOne(s => s.SupplierType)
            .WithMany()
            .HasForeignKey(s => s.SupplierTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Supplier>()
            .HasOne(s => s.TaxpayerCountry)
            .WithMany()
            .HasForeignKey(s => s.TaxpayerCountryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Supplier>()
            .HasOne(s => s.CountryName)
            .WithMany()
            .HasForeignKey(s => s.CountryNameId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Supplier>()
            .HasOne(s => s.Country)
            .WithMany()
            .HasForeignKey(s => s.CountryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Supplier>()
            .HasOne(s => s.BusinessRelationship)
            .WithMany()
            .HasForeignKey(s => s.BusinessRelationshipId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Supplier>()
            .HasOne(s => s.Province)
            .WithMany()
            .HasForeignKey(s => s.ProvinceId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Supplier>()
            .HasOne(s => s.VatCode)
            .WithMany()
            .HasForeignKey(s => s.VatCodeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Supplier>()
            .HasOne(s => s.PaymentMethod)
            .WithMany()
            .HasForeignKey(s => s.PaymentMethodId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Supplier>()
            .HasOne(s => s.PaymentTerms)
            .WithMany()
            .HasForeignKey(s => s.PaymentTermsId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Supplier>()
            .HasOne(s => s.Prefix)
            .WithMany()
            .HasForeignKey(s => s.PrefixId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Supplier>()
            .HasOne(s => s.Currency)
            .WithMany()
            .HasForeignKey(s => s.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Supplier>()
            .HasOne(s => s.PhoneCountryCode)
            .WithMany()
            .HasForeignKey(s => s.PhoneCountryCodeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Supplier>()
            .HasOne(s => s.MobileCountryCode)
            .WithMany()
            .HasForeignKey(s => s.MobileCountryCodeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Supplier>()
            .HasOne(s => s.CommunicationMethod)
            .WithMany()
            .HasForeignKey(s => s.CommunicationMethodId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Supplier>()
            .HasOne(s => s.AccountCountryCode)
            .WithMany()
            .HasForeignKey(s => s.AccountCountryCodeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Supplier>()
            .HasOne(s => s.AccountCurrencyCode)
            .WithMany()
            .HasForeignKey(s => s.AccountCurrencyCodeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Supplier>()
            .HasOne(s => s.Bank)
            .WithMany()
            .HasForeignKey(s => s.BankId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Supplier>()
            .HasOne(s => s.BankBranch)
            .WithMany()
            .HasForeignKey(s => s.BankBranchId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Supplier>()
            .HasOne(s => s.Status)
            .WithMany()
            .HasForeignKey(s => s.StatusId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Supplier>()
            .HasOne(s => s.VatRegistrationType)
            .WithMany()
            .HasForeignKey(s => s.VatRegistrationTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Supplier>()
            .HasOne(s => s.BusinessPartnerType)
            .WithMany()
            .HasForeignKey(s => s.BusinessPartnerTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Supplier>()
            .HasOne(s => s.BusinessLocationType)
            .WithMany()
            .HasForeignKey(s => s.BusinessLocationTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Supplier>()
            .HasOne(s => s.EInvoicingWave)
            .WithMany()
            .HasForeignKey(s => s.EInvoicingWaveId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Supplier>()
            .HasOne(s => s.TradeLicenseIssuingBody)
            .WithMany()
            .HasForeignKey(s => s.TradeLicenseIssuingBodyId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Supplier>()
            .HasOne(s => s.IntermediaryCountry)
            .WithMany()
            .HasForeignKey(s => s.IntermediaryCountryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<BankBranch>()
            .HasOne(b => b.Bank)
            .WithMany(b => b.Branches)
            .HasForeignKey(b => b.BankId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<LookupValue>()
            .HasIndex(l => new { l.Category, l.Code })
            .IsUnique();

        modelBuilder.Entity<SupplierFieldSetting>()
            .HasIndex(f => f.FieldKey)
            .IsUnique();

        // Default true so that when this column is added to an existing database, every already-seeded
        // field stays visible to suppliers (rather than defaulting to hidden).
        modelBuilder.Entity<SupplierFieldSetting>()
            .Property(f => f.IsVisibleToSupplier)
            .HasDefaultValue(true);

        // Owned child rows of a specific Supplier (not shared master data), so cascade-delete
        // here is intentional and diverges from the Restrict pattern used above.
        modelBuilder.Entity<SupplierAccessToken>()
            .HasOne(t => t.Supplier)
            .WithMany()
            .HasForeignKey(t => t.SupplierId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SupplierAccessToken>()
            .HasIndex(t => t.TokenHash)
            .IsUnique();

        modelBuilder.Entity<SupplierFieldChange>()
            .HasOne(c => c.Supplier)
            .WithMany()
            .HasForeignKey(c => c.SupplierId)
            .OnDelete(DeleteBehavior.Cascade);

        // NoAction (not SetNull): Supplier already cascades directly to SupplierFieldChange, and a
        // second cascading path via SupplierAccessToken would give SQL Server two ways to touch the
        // same table from one Supplier delete ("multiple cascade paths"), which it rejects outright.
        // SupplierAccessToken rows are never deleted by app logic, so this FK never actually fires.
        modelBuilder.Entity<SupplierFieldChange>()
            .HasOne(c => c.SupplierAccessToken)
            .WithMany()
            .HasForeignKey(c => c.SupplierAccessTokenId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<SupplierFieldChange>()
            .HasIndex(c => new { c.SupplierId, c.IsAcknowledged });

        modelBuilder.Entity<SupplierDocument>()
            .HasOne(d => d.Supplier)
            .WithMany()
            .HasForeignKey(d => d.SupplierId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SupplierDocument>()
            .HasIndex(d => new { d.SupplierId, d.DocumentType });

        // Owned child rows of a specific Supplier (partners read from its Trade License),
        // so cascade-delete matches the SupplierDocument pattern above.
        modelBuilder.Entity<SupplierPartner>()
            .HasOne(p => p.Supplier)
            .WithMany()
            .HasForeignKey(p => p.SupplierId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SupplierPartner>()
            .HasIndex(p => p.SupplierId);
    }
}
