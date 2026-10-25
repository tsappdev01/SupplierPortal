using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixSupplierPartnerRoleColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The SupplierPartners.Role column is in the model/snapshot but was missing on databases whose
            // SupplierPartners table was created before Role was added to the table-creation migration. Add
            // it only when absent, so this is safe on both those databases and fresh ones (where the
            // original migration already created the column).
            migrationBuilder.Sql(
                "IF COL_LENGTH('SupplierPartners', 'Role') IS NULL " +
                "ALTER TABLE [SupplierPartners] ADD [Role] nvarchar(80) NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "IF COL_LENGTH('SupplierPartners', 'Role') IS NOT NULL " +
                "ALTER TABLE [SupplierPartners] DROP COLUMN [Role];");
        }
    }
}
