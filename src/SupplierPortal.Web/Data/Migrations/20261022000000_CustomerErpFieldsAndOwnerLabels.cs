using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Aligns the customer Account block of the import/export template with the requested Oracle Fusion
    /// order: renames the Owner passport-country labels, drops "Owner 2 - Passport Issue Place/Country" (kept for
    /// Owner 1 only), and appends three ERP identifier columns after Payment Terms — Party ID, Party Number,
    /// ERP Bill To Account. Data + schema; reversible.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261022000000_CustomerErpFieldsAndOwnerLabels")]
    public partial class CustomerErpFieldsAndOwnerLabels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "PartyId", table: "Customers", type: "nvarchar(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>(name: "PartyNumber", table: "Customers", type: "nvarchar(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>(name: "ErpBillToAccount", table: "Customers", type: "nvarchar(100)", maxLength: 100, nullable: true);

            // Owner passport label alignment.
            migrationBuilder.Sql("UPDATE dbo.CustomerFieldConfigs SET DisplayLabel = N'Owner 1 - Passport Issue Country Code' WHERE Section = N'Account' AND FieldKey = N'Owner1PassportIssueCountry';");
            migrationBuilder.Sql("UPDATE dbo.CustomerFieldConfigs SET DisplayLabel = N'Owner 1 - Passport Issue Place/Country Name' WHERE Section = N'Account' AND FieldKey = N'Owner1PassportIssuePlaceCountry';");
            migrationBuilder.Sql("UPDATE dbo.CustomerFieldConfigs SET DisplayLabel = N'Owner 2 - Passport Issue Country Code' WHERE Section = N'Account' AND FieldKey = N'Owner2PassportIssueCountry';");

            // Owner 2 keeps only the Country Code column; remove its Place/Country column from the template.
            migrationBuilder.Sql("UPDATE dbo.CustomerFieldConfigs SET IsRemoved = 1, IsVisible = 0 WHERE Section = N'Account' AND FieldKey = N'Owner2PassportIssuePlaceCountry';");

            // Append the three ERP identifier fields after Payment Terms (end of the Account block).
            migrationBuilder.Sql(@"
DECLARE @seq int = (SELECT Sequence FROM dbo.CustomerFieldConfigs WHERE Section = N'Account' AND FieldKey = N'PaymentTerms');
DECLARE @grp nvarchar(80) = (SELECT GroupName FROM dbo.CustomerFieldConfigs WHERE Section = N'Account' AND FieldKey = N'PaymentTerms');
IF @seq IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.CustomerFieldConfigs WHERE Section = N'Account' AND FieldKey = N'PartyId')
BEGIN
    UPDATE dbo.CustomerFieldConfigs SET Sequence = Sequence + 3 WHERE Section = N'Account' AND Sequence > @seq;
    INSERT INTO dbo.CustomerFieldConfigs
        ([FieldKey],[DisplayLabel],[Section],[GroupName],[Sequence],[FieldKind],[BusinessControl],[CustomerControl],[IsVisible],[IsRequired],[IsRemoved])
    VALUES
        (N'PartyId', N'Party ID', N'Account', ISNULL(@grp, N''), @seq + 1, N'text', N'Editable', N'Editable', 1, 0, 0),
        (N'PartyNumber', N'Party Number', N'Account', ISNULL(@grp, N''), @seq + 2, N'text', N'Editable', N'Editable', 1, 0, 0),
        (N'ErpBillToAccount', N'ERP Bill To Account', N'Account', ISNULL(@grp, N''), @seq + 3, N'text', N'Editable', N'Editable', 1, 0, 0);
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DECLARE @seq int = (SELECT MIN(Sequence) FROM dbo.CustomerFieldConfigs WHERE Section = N'Account' AND FieldKey = N'PartyId');
DELETE FROM dbo.CustomerFieldConfigs WHERE Section = N'Account' AND FieldKey IN (N'PartyId', N'PartyNumber', N'ErpBillToAccount');
IF @seq IS NOT NULL
    UPDATE dbo.CustomerFieldConfigs SET Sequence = Sequence - 3 WHERE Section = N'Account' AND Sequence > @seq + 2;");

            migrationBuilder.Sql("UPDATE dbo.CustomerFieldConfigs SET IsRemoved = 0, IsVisible = 1 WHERE Section = N'Account' AND FieldKey = N'Owner2PassportIssuePlaceCountry';");
            migrationBuilder.Sql("UPDATE dbo.CustomerFieldConfigs SET DisplayLabel = N'Owner 2 - Passport Issue Country' WHERE Section = N'Account' AND FieldKey = N'Owner2PassportIssueCountry';");
            migrationBuilder.Sql("UPDATE dbo.CustomerFieldConfigs SET DisplayLabel = N'Owner 1 - Passport Issue Place/Country' WHERE Section = N'Account' AND FieldKey = N'Owner1PassportIssuePlaceCountry';");
            migrationBuilder.Sql("UPDATE dbo.CustomerFieldConfigs SET DisplayLabel = N'Owner 1 - Passport Issue Country' WHERE Section = N'Account' AND FieldKey = N'Owner1PassportIssueCountry';");

            migrationBuilder.DropColumn(name: "ErpBillToAccount", table: "Customers");
            migrationBuilder.DropColumn(name: "PartyNumber", table: "Customers");
            migrationBuilder.DropColumn(name: "PartyId", table: "Customers");
        }
    }
}
