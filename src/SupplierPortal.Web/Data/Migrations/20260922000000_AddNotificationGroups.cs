using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Adds Notification Groups: a named group of users used as a notification recipient list,
    /// with a many-to-many link to Users (NotificationGroupUsers). Seeds an "ALL_USERS" group and, when the
    /// user exists, adds nayyar@techsource.ae to it.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260922000000_AddNotificationGroups")]
    public partial class AddNotificationGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotificationGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<System.DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_NotificationGroups", x => x.Id));

            migrationBuilder.CreateTable(
                name: "NotificationGroupUsers",
                columns: table => new
                {
                    NotificationGroupId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationGroupUsers", x => new { x.NotificationGroupId, x.UserId });
                    table.ForeignKey(
                        name: "FK_NotificationGroupUsers_NotificationGroups_NotificationGroupId",
                        column: x => x.NotificationGroupId,
                        principalTable: "NotificationGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NotificationGroupUsers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationGroups_Name",
                table: "NotificationGroups",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationGroupUsers_UserId",
                table: "NotificationGroupUsers",
                column: "UserId");

            // Seed the ALL_USERS group and add the initial member if that user exists. Idempotent so a
            // re-run (or an environment where the user is absent) is harmless.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [NotificationGroups] WHERE [Name] = 'ALL_USERS')
    INSERT INTO [NotificationGroups] ([Name],[CreatedAt]) VALUES ('ALL_USERS', SYSUTCDATETIME());

INSERT INTO [NotificationGroupUsers] ([NotificationGroupId],[UserId])
SELECT g.[Id], u.[Id]
FROM [NotificationGroups] g
CROSS JOIN [Users] u
WHERE g.[Name] = 'ALL_USERS'
  AND u.[Email] = 'nayyar@techsource.ae'
  AND NOT EXISTS (SELECT 1 FROM [NotificationGroupUsers] x WHERE x.[NotificationGroupId] = g.[Id] AND x.[UserId] = u.[Id]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "NotificationGroupUsers");
            migrationBuilder.DropTable(name: "NotificationGroups");
        }
    }
}
