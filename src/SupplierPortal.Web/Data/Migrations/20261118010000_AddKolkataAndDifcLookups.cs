using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <inheritdoc />
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(SupplierPortal.Web.Data.ApplicationDbContext))]
    [Microsoft.EntityFrameworkCore.Migrations.Migration("20261118010000_AddKolkataAndDifcLookups")]
    public partial class AddKolkataAndDifcLookups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add master-data values that existing supplier uploads referenced but weren't yet seeded.
            // The seeder only populates an empty category, so these inserts bring already-seeded
            // databases up to date. Category ids: Province = 5, LicensingAuthority = 18. Guarded so the
            // migration is idempotent and does not duplicate a value an admin may have already added.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM LookupValues WHERE Category = 5 AND DisplayName = N'Kolkata')
    INSERT INTO LookupValues (Category, Code, DisplayName, IsActive) VALUES (5, N'Kolkata', N'Kolkata', 1);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM LookupValues WHERE Category = 18 AND Code = N'DIFC')
    INSERT INTO LookupValues (Category, Code, DisplayName, IsActive) VALUES (18, N'DIFC', N'Dubai International Financial Centre (DIFC)', 1);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM LookupValues WHERE Category = 5 AND Code = N'Kolkata';");
            migrationBuilder.Sql("DELETE FROM LookupValues WHERE Category = 18 AND Code = N'DIFC';");
        }
    }
}
