using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <inheritdoc />
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(SupplierPortal.Web.Data.ApplicationDbContext))]
    [Microsoft.EntityFrameworkCore.Migrations.Migration("20261118000000_AddIncorporationAndInternalReview")]
    public partial class AddIncorporationAndInternalReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<System.DateTime>(
                name: "DateOfIncorporation",
                table: "Suppliers",
                type: "datetime2",
                nullable: true);
            migrationBuilder.AddColumn<bool>(
                name: "IntAttachmentsReviewed",
                table: "Suppliers",
                type: "bit",
                nullable: true);
            migrationBuilder.AddColumn<bool>(
                name: "IntAdverseMedia",
                table: "Suppliers",
                type: "bit",
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "IntAdverseMediaDetails",
                table: "Suppliers",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "IntResponseComments",
                table: "Suppliers",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "IntMeetingType",
                table: "Suppliers",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "IntMeetingRepName",
                table: "Suppliers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
            migrationBuilder.AddColumn<System.DateTime>(
                name: "IntMeetingDate",
                table: "Suppliers",
                type: "datetime2",
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "IntMeetingDiRep",
                table: "Suppliers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "IntPobMethod",
                table: "Suppliers",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);
            migrationBuilder.AddColumn<bool>(
                name: "IntPobAddressMatch",
                table: "Suppliers",
                type: "bit",
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "IntPobRepName",
                table: "Suppliers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
            migrationBuilder.AddColumn<System.DateTime>(
                name: "IntPobDate",
                table: "Suppliers",
                type: "datetime2",
                nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "IntPobDiRep",
                table: "Suppliers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
            migrationBuilder.AddColumn<bool>(
                name: "IntPobActivitiesCompatible",
                table: "Suppliers",
                type: "bit",
                nullable: true);
            migrationBuilder.AddColumn<bool>(
                name: "IntTransactionsProportionate",
                table: "Suppliers",
                type: "bit",
                nullable: true);
            migrationBuilder.AddColumn<bool>(
                name: "IntActivitiesReviewed",
                table: "Suppliers",
                type: "bit",
                nullable: true);
            migrationBuilder.AddColumn<bool>(
                name: "IntDeclarationReviewed",
                table: "Suppliers",
                type: "bit",
                nullable: true);
            migrationBuilder.AddColumn<bool>(
                name: "IsReviewed",
                table: "SupplierDocuments",
                type: "bit",
                nullable: false,
                defaultValue: false);
            migrationBuilder.AddColumn<string>(
                name: "ReviewedBy",
                table: "SupplierDocuments",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);
            migrationBuilder.AddColumn<System.DateTime>(
                name: "ReviewedAt",
                table: "SupplierDocuments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE dbo.SupplierFieldSettings SET DisplayLabel = N'Has the manager reflected on the trade license changed more than twice over the previous 12 months?' " +
                "WHERE FieldKey = N'KysRepsChangedTwice' AND DisplayLabel = N'Have the supplier''s key employees or persons authorised to represent the supplier changed more than twice in the last 12 months?';");
            migrationBuilder.Sql(
                "UPDATE dbo.SupplierFieldSettings SET DisplayLabel = N'Emirates ID / Passport No. of the Director, Agent or Employee Authorized to Represent the Supplier' " +
                "WHERE FieldKey = N'KysIdNumber' AND DisplayLabel = N'Emirates ID / Passport No. of the Person Authorized to Represent the Supplier';");
            migrationBuilder.Sql(
                "UPDATE dbo.SupplierFieldSettings SET DisplayLabel = N'Please indicate the documentation available to support the source and authenticity of the goods.' " +
                "WHERE FieldKey = N'KysSupportingDocs' AND DisplayLabel = N'What supporting documentation is available?';");
            migrationBuilder.Sql(
                "UPDATE dbo.SupplierFieldSettings SET IsMandatory = 1 WHERE FieldKey IN (N'SupplierTin', N'PeppolId', N'ApprovedServiceProviderId');");
            migrationBuilder.Sql(
                "UPDATE dbo.SupplierFieldSettings SET IsMandatory = 1 WHERE Step = 6;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DateOfIncorporation",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "IntAttachmentsReviewed",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "IntAdverseMedia",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "IntAdverseMediaDetails",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "IntResponseComments",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "IntMeetingType",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "IntMeetingRepName",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "IntMeetingDate",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "IntMeetingDiRep",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "IntPobMethod",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "IntPobAddressMatch",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "IntPobRepName",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "IntPobDate",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "IntPobDiRep",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "IntPobActivitiesCompatible",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "IntTransactionsProportionate",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "IntActivitiesReviewed",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "IntDeclarationReviewed",
                table: "Suppliers");
            migrationBuilder.DropColumn(
                name: "IsReviewed",
                table: "SupplierDocuments");
            migrationBuilder.DropColumn(
                name: "ReviewedBy",
                table: "SupplierDocuments");
            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "SupplierDocuments");
        }
    }
}
