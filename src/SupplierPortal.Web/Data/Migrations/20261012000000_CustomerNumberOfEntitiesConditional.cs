using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Places "Number of Entities" directly after "VAT Structure" (moves it into the same Tax group,
    /// keeping sequence 8 between VAT Structure = 7 and Customer/Buyer TIN = 9) and makes it conditional: it is
    /// shown only when VAT Structure = "VAT Group", otherwise hidden (and cleared on save). Customer portal.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261012000000_CustomerNumberOfEntitiesConditional")]
    public partial class CustomerNumberOfEntitiesConditional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(@"
UPDATE dbo.CustomerFieldConfigs
   SET GroupName = N'Tax, Trade License & Registration',
       Sequence = 8,
       ControllingFieldKey = N'VATStructure',
       ControllingValue = N'VAT Group'
 WHERE Section = N'Account' AND FieldKey = N'NumberOfEntities';");

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(@"
UPDATE dbo.CustomerFieldConfigs
   SET GroupName = N'Company Identity & Classification',
       ControllingFieldKey = NULL,
       ControllingValue = NULL
 WHERE Section = N'Account' AND FieldKey = N'NumberOfEntities';");
    }
}
