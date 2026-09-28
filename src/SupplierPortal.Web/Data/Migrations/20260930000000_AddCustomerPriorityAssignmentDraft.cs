using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Brings the Customer record to Supplier parity for the dashboard: adds Priority (P1..P4),
    /// AssignedToUserId (owner, FK to Users, SET NULL on delete) and IsDraft. Existing rows default to
    /// IsDraft = 0 so already-entered customers are not treated as drafts.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260930000000_AddCustomerPriorityAssignmentDraft")]
    public partial class AddCustomerPriorityAssignmentDraft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Priority",
                table: "Customers",
                type: "nvarchar(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AssignedToUserId",
                table: "Customers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDraft",
                table: "Customers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_AssignedToUserId",
                table: "Customers",
                column: "AssignedToUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Users_AssignedToUserId",
                table: "Customers",
                column: "AssignedToUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Customers_Users_AssignedToUserId", table: "Customers");
            migrationBuilder.DropIndex(name: "IX_Customers_AssignedToUserId", table: "Customers");
            migrationBuilder.DropColumn(name: "IsDraft", table: "Customers");
            migrationBuilder.DropColumn(name: "AssignedToUserId", table: "Customers");
            migrationBuilder.DropColumn(name: "Priority", table: "Customers");
        }
    }
}
