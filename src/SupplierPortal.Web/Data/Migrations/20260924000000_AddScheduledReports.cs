using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Adds the ScheduledReports table: administrator-defined schedules that email a supplier
    /// report (PDF/Excel) as an attachment to a notification group's active members on a recurring
    /// date/time and frequency.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260924000000_AddScheduledReports")]
    public partial class AddScheduledReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScheduledReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Format = table.Column<int>(type: "int", nullable: false),
                    Frequency = table.Column<int>(type: "int", nullable: false),
                    NextRunUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    BusinessUnitId = table.Column<int>(type: "int", nullable: true),
                    NotificationGroupId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    LastRunUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastRunStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    LastRunDetail = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduledReports_NotificationGroups_NotificationGroupId",
                        column: x => x.NotificationGroupId,
                        principalTable: "NotificationGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledReports_NotificationGroupId",
                table: "ScheduledReports",
                column: "NotificationGroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ScheduledReports");
        }
    }
}
