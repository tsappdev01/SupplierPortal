using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Adds the ReportDefinitions table (reusable Report Builder definitions: data source, columns,
    /// filters, title and format) and links ScheduledReports to a definition via a nullable ReportDefinitionId.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260925000000_AddReportDefinitions")]
    public partial class AddReportDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReportDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SchemaName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TableName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ColumnsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FiltersJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Format = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportDefinitions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReportDefinitions_Name",
                table: "ReportDefinitions",
                column: "Name",
                unique: true);

            migrationBuilder.AddColumn<int>(
                name: "ReportDefinitionId",
                table: "ScheduledReports",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledReports_ReportDefinitionId",
                table: "ScheduledReports",
                column: "ReportDefinitionId");

            migrationBuilder.AddForeignKey(
                name: "FK_ScheduledReports_ReportDefinitions_ReportDefinitionId",
                table: "ScheduledReports",
                column: "ReportDefinitionId",
                principalTable: "ReportDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ScheduledReports_ReportDefinitions_ReportDefinitionId",
                table: "ScheduledReports");

            migrationBuilder.DropIndex(
                name: "IX_ScheduledReports_ReportDefinitionId",
                table: "ScheduledReports");

            migrationBuilder.DropColumn(
                name: "ReportDefinitionId",
                table: "ScheduledReports");

            migrationBuilder.DropTable(name: "ReportDefinitions");
        }
    }
}
