using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Adds the ScheduledReportRuns table — a per-report execution history used for the run-history
    /// timeline on the Scheduled Reports admin page.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260927000000_AddScheduledReportRuns")]
    public partial class AddScheduledReportRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScheduledReportRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScheduledReportId = table.Column<int>(type: "int", nullable: false),
                    RunAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledReportRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduledReportRuns_ScheduledReports_ScheduledReportId",
                        column: x => x.ScheduledReportId,
                        principalTable: "ScheduledReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledReportRuns_ScheduledReportId_RunAtUtc",
                table: "ScheduledReportRuns",
                columns: ["ScheduledReportId", "RunAtUtc"]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ScheduledReportRuns");
        }
    }
}
