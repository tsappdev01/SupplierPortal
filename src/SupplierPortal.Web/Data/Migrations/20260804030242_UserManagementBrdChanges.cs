using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class UserManagementBrdChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Designation",
                table: "Users",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "Logo",
                table: "BusinessUnits",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoContentType",
                table: "BusinessUnits",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActorUsername = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    Details = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserBusinessUnits",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    BusinessUnitId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBusinessUnits", x => new { x.UserId, x.BusinessUnitId });
                    table.ForeignKey(
                        name: "FK_UserBusinessUnits_BusinessUnits_BusinessUnitId",
                        column: x => x.BusinessUnitId,
                        principalTable: "BusinessUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserBusinessUnits_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserBusinessUnits_BusinessUnitId",
                table: "UserBusinessUnits",
                column: "BusinessUnitId");

            // Carry forward each user's existing single Business Unit assignment into the new
            // many-to-many join table before the old column is dropped, so no data is lost.
            migrationBuilder.Sql(
                "INSERT INTO [UserBusinessUnits] ([UserId], [BusinessUnitId]) " +
                "SELECT [Id], [BusinessUnitId] FROM [Users] WHERE [BusinessUnitId] IS NOT NULL;");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_BusinessUnits_BusinessUnitId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_BusinessUnitId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "BusinessUnitId",
                table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BusinessUnitId",
                table: "Users",
                type: "int",
                nullable: true);

            // Best-effort restore: a user assigned to multiple Business Units can only keep one on
            // the old single-FK column, so the lowest BusinessUnitId per user is kept.
            migrationBuilder.Sql(
                "UPDATE u SET u.[BusinessUnitId] = ub.MinBusinessUnitId " +
                "FROM [Users] u " +
                "INNER JOIN (SELECT [UserId], MIN([BusinessUnitId]) AS MinBusinessUnitId FROM [UserBusinessUnits] GROUP BY [UserId]) ub " +
                "ON ub.[UserId] = u.[Id];");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "UserBusinessUnits");

            migrationBuilder.DropColumn(
                name: "Designation",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Logo",
                table: "BusinessUnits");

            migrationBuilder.DropColumn(
                name: "LogoContentType",
                table: "BusinessUnits");

            migrationBuilder.CreateIndex(
                name: "IX_Users_BusinessUnitId",
                table: "Users",
                column: "BusinessUnitId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_BusinessUnits_BusinessUnitId",
                table: "Users",
                column: "BusinessUnitId",
                principalTable: "BusinessUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
