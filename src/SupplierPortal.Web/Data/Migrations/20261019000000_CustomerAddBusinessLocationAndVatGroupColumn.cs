using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Adds the two remaining "Customer Data Template" columns that had no backing field, so the
    /// customer upload/download template matches the business sheet exactly (Oracle Fusion round-trip):
    ///   - "Business Location Type" (sheet col 26) — a new lookup field on Customer (Mainland / Free Zone),
    ///     inserted after "Authority Code".
    ///   - "Legal Names of all Entities under the VAT Group" (sheet col 11) — a synthetic Account template
    ///     column keyed 'VatGroupLegalNames' inserted after "Number of Entities". It has no scalar property;
    ///     the export joins the CustomerVatGroupEntity legal names into it and the import splits them back,
    ///     and the interactive form skips it (the VAT Group grid captures that data there).
    /// Sequences of the following account fields are shifted to open each slot; the Down reverses everything.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261019000000_CustomerAddBusinessLocationAndVatGroupColumn")]
    public partial class CustomerAddBusinessLocationAndVatGroupColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BusinessLocationType", table: "Customers", type: "nvarchar(100)", maxLength: 100, nullable: true);

            // Insert the VAT-group legal-names template column right after "Number of Entities" (sheet col 11).
            migrationBuilder.Sql(@"
DECLARE @seq int = (SELECT Sequence FROM dbo.CustomerFieldConfigs WHERE Section = N'Account' AND FieldKey = N'NumberOfEntities');
DECLARE @grp nvarchar(80) = (SELECT GroupName FROM dbo.CustomerFieldConfigs WHERE Section = N'Account' AND FieldKey = N'NumberOfEntities');
IF @seq IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.CustomerFieldConfigs WHERE Section = N'Account' AND FieldKey = N'VatGroupLegalNames')
BEGIN
    UPDATE dbo.CustomerFieldConfigs SET Sequence = Sequence + 1 WHERE Section = N'Account' AND Sequence > @seq;
    INSERT INTO dbo.CustomerFieldConfigs
        ([FieldKey],[DisplayLabel],[Section],[GroupName],[Sequence],[FieldKind],[BusinessControl],[CustomerControl],[IsVisible],[IsRequired],[IsRemoved])
    VALUES (N'VatGroupLegalNames', N'Legal Names of all Entities under the VAT Group', N'Account', ISNULL(@grp, N''), @seq + 1, N'text', N'Editable', N'Editable', 1, 0, 0);
END");

            // Insert the Business Location Type lookup field right after "Authority Code" (sheet col 26).
            migrationBuilder.Sql(@"
DECLARE @seq int = (SELECT Sequence FROM dbo.CustomerFieldConfigs WHERE Section = N'Account' AND FieldKey = N'AuthorityCode');
DECLARE @grp nvarchar(80) = (SELECT GroupName FROM dbo.CustomerFieldConfigs WHERE Section = N'Account' AND FieldKey = N'AuthorityCode');
IF @seq IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.CustomerFieldConfigs WHERE Section = N'Account' AND FieldKey = N'BusinessLocationType')
BEGIN
    UPDATE dbo.CustomerFieldConfigs SET Sequence = Sequence + 1 WHERE Section = N'Account' AND Sequence > @seq;
    INSERT INTO dbo.CustomerFieldConfigs
        ([FieldKey],[DisplayLabel],[Section],[GroupName],[Sequence],[FieldKind],[BusinessControl],[CustomerControl],[IsVisible],[IsRequired],[IsRemoved])
    VALUES (N'BusinessLocationType', N'Business Location Type', N'Account', ISNULL(@grp, N''), @seq + 1, N'lookup', N'Lookup', N'Lookup', 1, 0, 0);
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove Business Location Type and close its sequence gap.
            migrationBuilder.Sql(@"
DECLARE @seq int = (SELECT Sequence FROM dbo.CustomerFieldConfigs WHERE Section = N'Account' AND FieldKey = N'BusinessLocationType');
IF @seq IS NOT NULL
BEGIN
    DELETE FROM dbo.CustomerFieldConfigs WHERE Section = N'Account' AND FieldKey = N'BusinessLocationType';
    UPDATE dbo.CustomerFieldConfigs SET Sequence = Sequence - 1 WHERE Section = N'Account' AND Sequence > @seq;
END");

            // Remove the VAT-group legal-names column and close its sequence gap.
            migrationBuilder.Sql(@"
DECLARE @seq int = (SELECT Sequence FROM dbo.CustomerFieldConfigs WHERE Section = N'Account' AND FieldKey = N'VatGroupLegalNames');
IF @seq IS NOT NULL
BEGIN
    DELETE FROM dbo.CustomerFieldConfigs WHERE Section = N'Account' AND FieldKey = N'VatGroupLegalNames';
    UPDATE dbo.CustomerFieldConfigs SET Sequence = Sequence - 1 WHERE Section = N'Account' AND Sequence > @seq;
END");

            migrationBuilder.DropColumn(name: "BusinessLocationType", table: "Customers");
        }
    }
}
