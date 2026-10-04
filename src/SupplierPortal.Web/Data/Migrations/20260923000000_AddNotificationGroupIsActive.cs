using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Adds an enabled/disabled flag to notification groups (disabled groups are kept but skipped
    /// when sending notifications). Existing groups default to enabled.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260923000000_AddNotificationGroupIsActive")]
    public partial class AddNotificationGroupIsActive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "NotificationGroups",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "IsActive", table: "NotificationGroups");
        }
    }
}
