using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Adds WeekDaysMask to ScheduledReports so a Weekly schedule can run on specific days of the
    /// week (a bitmask of DayOfWeek). 0 keeps the legacy "every 7 days from the start" behaviour.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260928000000_AddScheduledReportWeekDays")]
    public partial class AddScheduledReportWeekDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WeekDaysMask",
                table: "ScheduledReports",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "WeekDaysMask", table: "ScheduledReports");
        }
    }
}
