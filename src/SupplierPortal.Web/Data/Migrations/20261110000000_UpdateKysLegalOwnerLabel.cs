using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <inheritdoc />
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(SupplierPortal.Web.Data.ApplicationDbContext))]
    [Microsoft.EntityFrameworkCore.Migrations.Migration("20261110000000_UpdateKysLegalOwnerLabel")]
    public partial class UpdateKysLegalOwnerLabel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Relabel the legal-owner-details field. Guarded on the old label so an admin edit is kept.
            migrationBuilder.Sql(
                "UPDATE dbo.SupplierFieldSettings SET DisplayLabel = N'Please provide details of the legal owner of the goods and explain your role in the supply.' " +
                "WHERE FieldKey = N'KysLegalOwnerDetails' AND DisplayLabel = N'Please provide the legal owner''s details and explain the supplier''s role in the supply';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // One-way relabel; no down migration.
        }
    }
}
