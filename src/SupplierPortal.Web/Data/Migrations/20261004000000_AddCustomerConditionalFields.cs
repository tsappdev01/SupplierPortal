using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Adds conditional-field support to CustomerFieldConfigs (ControllingFieldKey + ControllingValue)
    /// so a field can be shown/required only when a controlling Yes/No field has a given value. Pre-seeds the
    /// two known rules: Power of Attorney (text) depends on the Power of Attorney (Yes/No) toggle, and TRN
    /// Number depends on TRN Applicable — both shown + required only when the toggle is Yes.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261004000000_AddCustomerConditionalFields")]
    public partial class AddCustomerConditionalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ControllingFieldKey", table: "CustomerFieldConfigs",
                type: "nvarchar(100)", maxLength: 100, nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "ControllingValue", table: "CustomerFieldConfigs",
                type: "nvarchar(40)", maxLength: 40, nullable: true);

            // Pre-configure the two requested rules (shown + required only when the controlling toggle = Yes).
            migrationBuilder.Sql(@"
UPDATE dbo.CustomerFieldConfigs
   SET ControllingFieldKey = 'PowerOfAttorney', ControllingValue = 'true', IsRequired = 1
 WHERE FieldKey = 'PowerOfAttorney2';");
            migrationBuilder.Sql(@"
UPDATE dbo.CustomerFieldConfigs
   SET ControllingFieldKey = 'TRNApplicable', ControllingValue = 'true', IsRequired = 1
 WHERE FieldKey = 'TRNNumber';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ControllingValue", table: "CustomerFieldConfigs");
            migrationBuilder.DropColumn(name: "ControllingFieldKey", table: "CustomerFieldConfigs");
        }
    }
}
