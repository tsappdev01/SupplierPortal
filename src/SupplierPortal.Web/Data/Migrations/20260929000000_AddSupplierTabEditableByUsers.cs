using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Adds EditableByUserIdsCsv to SupplierTabSettings so a wizard tab can be restricted to a
    /// specific set of internal users on the internal supplier form (others see it read-only). Null/empty
    /// keeps the default: any internal user may edit the tab.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260929000000_AddSupplierTabEditableByUsers")]
    public partial class AddSupplierTabEditableByUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EditableByUserIdsCsv",
                table: "SupplierTabSettings",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "EditableByUserIdsCsv", table: "SupplierTabSettings");
        }
    }
}
