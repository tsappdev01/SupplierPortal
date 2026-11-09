using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <inheritdoc />
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(SupplierPortal.Web.Data.ApplicationDbContext))]
    [Microsoft.EntityFrameworkCore.Migrations.Migration("20261109010000_UpdateKysLabels")]
    public partial class UpdateKysLabels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Apply the KYS 2026 revision relabels to existing field-setting rows. Guarded on the old
            // label so an administrator's customised label is never overwritten.
            migrationBuilder.Sql(
                "UPDATE dbo.SupplierFieldSettings SET DisplayLabel = N'e-Invoicing TIN' " +
                "WHERE FieldKey = N'SupplierTin' AND DisplayLabel = N'Supplier TIN';");
            migrationBuilder.Sql(
                "UPDATE dbo.SupplierFieldSettings SET DisplayLabel = N'Emirates ID / Passport No. of the Person Authorized to Represent the Supplier' " +
                "WHERE FieldKey = N'KysIdNumber' AND DisplayLabel = N'Emirates ID / Passport No.';");
            migrationBuilder.Sql(
                "UPDATE dbo.SupplierFieldSettings SET DisplayLabel = N'Has the registered address (as per trade license) changed more than twice in the last 12 months?' " +
                "WHERE FieldKey = N'KysAddressChangedTwice' AND DisplayLabel = N'Has the business address changed more than twice in the last 12 months?';");
            migrationBuilder.Sql(
                "UPDATE dbo.SupplierFieldSettings SET DisplayLabel = N'Have the supplier''s key employees or persons authorised to represent the supplier changed more than twice in the last 12 months?' " +
                "WHERE FieldKey = N'KysRepsChangedTwice' AND DisplayLabel = N'Have the employees / representatives dealing with us changed more than twice in the last 12 months?';");
            migrationBuilder.Sql(
                "UPDATE dbo.SupplierFieldSettings SET DisplayLabel = N'Which activity listed on the supplier''s Trade License covers the goods or services being supplied?' " +
                "WHERE FieldKey = N'KysCoveringActivity' AND DisplayLabel = N'Which licensed activity (Section 1) covers this supply?';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // One-way data update; no down migration for the relabels.
        }
    }
}
