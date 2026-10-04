using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Adds the optional "assigned to" internal user (owner) on a supplier, referencing Users.
    /// On user deletion the assignment is cleared (SET NULL) rather than blocking the delete.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260916000000_AddSupplierAssignedToUser")]
    public partial class AddSupplierAssignedToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssignedToUserId",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_AssignedToUserId",
                table: "Suppliers",
                column: "AssignedToUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_Users_AssignedToUserId",
                table: "Suppliers",
                column: "AssignedToUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_Users_AssignedToUserId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_AssignedToUserId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "AssignedToUserId",
                table: "Suppliers");
        }
    }
}
