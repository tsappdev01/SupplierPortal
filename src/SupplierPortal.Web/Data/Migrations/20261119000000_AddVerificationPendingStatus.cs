using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <inheritdoc />
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(SupplierPortal.Web.Data.ApplicationDbContext))]
    [Microsoft.EntityFrameworkCore.Migrations.Migration("20261119000000_AddVerificationPendingStatus")]
    public partial class AddVerificationPendingStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Supplier workflow now has an internal verification step: a self-service submission goes to
            // "Verification Pending" instead of straight to "Completed". Category 13 = SupplierStatus.
            // Guarded + idempotent: relabel the retired "For Review" row if present, otherwise insert the
            // new status. Existing suppliers keep their current status.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM LookupValues WHERE Category = 13 AND DisplayName = N'For Review')
    UPDATE LookupValues SET DisplayName = N'Verification Pending', Code = N'Verification Pending'
        WHERE Category = 13 AND DisplayName = N'For Review';
ELSE IF NOT EXISTS (SELECT 1 FROM LookupValues WHERE Category = 13 AND DisplayName = N'Verification Pending')
    INSERT INTO LookupValues (Category, Code, DisplayName, IsActive)
        VALUES (13, N'Verification Pending', N'Verification Pending', 1);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE LookupValues SET DisplayName = N'For Review', Code = N'For Review'
    WHERE Category = 13 AND DisplayName = N'Verification Pending';");
        }
    }
}
