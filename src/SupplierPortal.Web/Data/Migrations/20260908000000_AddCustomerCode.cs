using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Adds the optional Customer Code field to a customer, plus its field-config row so it appears
    /// in the customer wizard (Company Identity &amp; Classification) and the field settings screen.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260908000000_AddCustomerCode")]
    public partial class AddCustomerCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerCode",
                table: "Customers",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            // Seed the field config so the wizard/settings render it. Guarded so a re-run is a no-op.
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM [CustomerFieldConfigs] WHERE [Section] = 'Account' AND [FieldKey] = 'CustomerCode')
                INSERT INTO [CustomerFieldConfigs]
                    ([FieldKey],[DisplayLabel],[Section],[GroupName],[Sequence],[FieldKind],[BusinessControl],[CustomerControl],[IsVisible],[IsRequired])
                VALUES
                    ('CustomerCode','Customer Code','Account','Company Identity & Classification',0,'text','Editable','Editable',1,0);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM [CustomerFieldConfigs] WHERE [Section] = 'Account' AND [FieldKey] = 'CustomerCode';");
            migrationBuilder.DropColumn(name: "CustomerCode", table: "Customers");
        }
    }
}
