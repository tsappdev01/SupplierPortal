using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Keeps the three ERP identifier columns (Party ID, Party Number, ERP Bill To Account) in the
    /// customer import/export template, appended after Payment Terms, for the Oracle Fusion data exchange. An
    /// earlier revision of <c>20261027_CustomerTemplateExact129Columns</c> removed them to hit an exact 129-column
    /// layout; the business still needs them, so this idempotently re-asserts them as visible / non-removed. It is
    /// a no-op where they were never removed. Their Sequence (PaymentTerms + 1/2/3, set by
    /// <c>20261022_CustomerErpFieldsAndOwnerLabels</c>) is unchanged, so they stay appended at the end of the
    /// Account block. Pure data; reversible.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261028000000_CustomerKeepErpTemplateColumns")]
    public partial class CustomerKeepErpTemplateColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE dbo.CustomerFieldConfigs SET IsRemoved = 0, IsVisible = 1 " +
                "WHERE Section = N'Account' AND FieldKey IN (N'PartyId', N'PartyNumber', N'ErpBillToAccount');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: whether the ERP columns are present is owned by the business/Field Manager, not this
            // migration. Reverting must not silently drop columns the Fusion exchange depends on.
        }
    }
}
