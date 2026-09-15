using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Adds a Content (varbinary) column to SupplierDocuments so uploads can be stored in the
    /// database (matching customer documents) when Azure Blob Storage is disabled.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260921000000_AddSupplierDocumentContent")]
    public partial class AddSupplierDocumentContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "Content", table: "SupplierDocuments", type: "varbinary(max)", nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Content", table: "SupplierDocuments");
        }
    }
}
