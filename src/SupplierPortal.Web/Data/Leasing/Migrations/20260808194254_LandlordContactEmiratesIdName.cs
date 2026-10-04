using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupplierPortal.Web.Data.Leasing.Migrations
{
    /// <inheritdoc />
    public partial class LandlordContactEmiratesIdName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmiratesIdName",
                schema: "leasing",
                table: "LandlordContacts",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmiratesIdName",
                schema: "leasing",
                table: "LandlordContacts");
        }
    }
}
