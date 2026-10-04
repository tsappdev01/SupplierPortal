using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPortalAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PortalAccess",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Backfill from the previous role-based behaviour so nobody loses access on upgrade:
            //   - every user could open the Supplier portal            -> grant Supplier (bit 1)
            //   - Administrators (1) and Leasing Managers (3) could     -> also grant Landlord (bit 2)
            //     open the Landlord portal
            migrationBuilder.Sql("UPDATE [Users] SET [PortalAccess] = [PortalAccess] | 1;");
            migrationBuilder.Sql("UPDATE [Users] SET [PortalAccess] = [PortalAccess] | 2 WHERE [UserType] IN (1, 3);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PortalAccess",
                table: "Users");
        }
    }
}
