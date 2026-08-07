using Microsoft.EntityFrameworkCore;
using SupplierPortal.Web.Models.Leasing;

namespace SupplierPortal.Web.Data.Leasing;

/// <summary>
/// Bounded-context DbContext for the Landlord / Leasing module. It reads the read-only land view
/// <c>dbo.Property</c> (<see cref="Premises"/>) and owns the <c>leasing.*</c> tables. Kept separate from
/// <see cref="ApplicationDbContext"/> — its own migrations history (<c>__EFLeasingMigrationsHistory</c>,
/// configured at registration) means it never shares a model snapshot with the supplier schema.
/// </summary>
public class LeasingDbContext(DbContextOptions<LeasingDbContext> options) : DbContext(options)
{
    public const string Schema = "leasing";

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

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Read-only land view — never migrated.
        b.Entity<Premises>(e =>
        {
            e.ToView("Property");            // [dbo].[Property]
            e.HasKey(p => p.Id);
            e.Property(p => p.Id).ValueGeneratedNever();
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
    }
}
