using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Adds the Departments table (seeded with the organization's departments) and links Users to
    /// a department (optional; cleared if the department is removed).</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260918000000_AddDepartments")]
    public partial class AddDepartments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_Departments", x => x.Id));

            migrationBuilder.AddColumn<int>(
                name: "DepartmentId",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_DepartmentId",
                table: "Users",
                column: "DepartmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Departments_DepartmentId",
                table: "Users",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Seed the departments (only if the table is empty, so re-running is safe).
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [Departments])
BEGIN
    INSERT INTO [Departments] ([Code],[Name],[IsActive]) VALUES
        ('RISK','Risk Management',1),
        ('FIN','Finance',1),
        ('GMDI','GM-DI Office',1),
        ('COO','COO Office',1),
        ('PR','Public Relations',1),
        ('BOD','BOD',1),
        ('CEO','CEO Office',1),
        ('IAC','Internal Audit & Controls',1),
        ('HR','HR & Admin',1),
        ('CORP','Corporate Affairs',1),
        ('MKTG','Marketing & Communications',1),
        ('PROJ','Projects',1),
        ('IT','IT',1),
        ('OPS','Operations',1),
        ('BIZDEV','Business Development',1),
        ('COMM','Commercial',1),
        ('LEGAL','Legal',1),
        ('SUBS','Subsidiary',1),
        ('COMPL','Compliance',1),
        ('GM','GM',1);
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Users_Departments_DepartmentId", table: "Users");
            migrationBuilder.DropIndex(name: "IX_Users_DepartmentId", table: "Users");
            migrationBuilder.DropColumn(name: "DepartmentId", table: "Users");
            migrationBuilder.DropTable(name: "Departments");
        }
    }
}
