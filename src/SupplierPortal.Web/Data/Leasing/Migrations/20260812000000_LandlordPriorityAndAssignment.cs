using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data.Leasing;

#nullable disable

namespace SupplierPortal.Web.Data.Leasing.Migrations
{
    /// <summary>Adds work Priority (P1..P4) and AssignedToUserId to Landlord registrations, for the dashboard
    /// priority/assigned-to filters and inline assignment (parity with the customer/supplier dashboards).</summary>
    [DbContext(typeof(LeasingDbContext))]
    [Migration("20260812000000_LandlordPriorityAndAssignment")]
    public partial class LandlordPriorityAndAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Priority",
                table: "Landlords",
                type: "nvarchar(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AssignedToUserId",
                table: "Landlords",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Priority", table: "Landlords");
            migrationBuilder.DropColumn(name: "AssignedToUserId", table: "Landlords");
        }
    }
}
