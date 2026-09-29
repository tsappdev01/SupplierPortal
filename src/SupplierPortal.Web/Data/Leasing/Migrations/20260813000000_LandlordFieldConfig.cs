using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data.Leasing;

#nullable disable

namespace SupplierPortal.Web.Data.Leasing.Migrations
{
    /// <summary>Adds the landlord Field/Tab configuration subsystem (parity with the customer Field Manager):
    /// leasing.LandlordFieldConfigs + leasing.LandlordTabSettings, seeded with the landlord primary-contact and
    /// company field catalog. The config drives how the internal detail and the self-service wizard render each
    /// field (label, help, visibility, required, min/max, conditional), with no code change.</summary>
    [DbContext(typeof(LeasingDbContext))]
    [Migration("20260813000000_LandlordFieldConfig")]
    public partial class LandlordFieldConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LandlordFieldConfigs",
                schema: "leasing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FieldKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    HelpText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MinLength = table.Column<int>(type: "int", nullable: true),
                    MaxLength = table.Column<int>(type: "int", nullable: true),
                    Section = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    GroupName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    FieldKind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BusinessControl = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LandlordControl = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    IsRemoved = table.Column<bool>(type: "bit", nullable: false),
                    ControllingFieldKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ControllingValue = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_LandlordFieldConfigs", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_LandlordFieldConfigs_Section_FieldKey",
                schema: "leasing",
                table: "LandlordFieldConfigs",
                columns: new[] { "Section", "FieldKey" },
                unique: true);

            migrationBuilder.CreateTable(
                name: "LandlordTabSettings",
                schema: "leasing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Section = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    GroupName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false),
                    EditableByUserIdsCsv = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table => table.PrimaryKey("PK_LandlordTabSettings", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_LandlordTabSettings_Section_GroupName",
                schema: "leasing",
                table: "LandlordTabSettings",
                columns: new[] { "Section", "GroupName" },
                unique: true);

            // ---- Seed the landlord field catalog (primary contact + company) ----
            var cols = new[]
            {
                "FieldKey", "DisplayLabel", "Section", "GroupName", "Sequence", "FieldKind",
                "BusinessControl", "LandlordControl", "IsVisible", "IsRequired", "IsRemoved"
            };
            object[] Row(string key, string label, string section, string group, int seq, string kind, bool required) =>
                new object[] { key, label, section, group, seq, kind, "Editable", "Editable", true, required, false };

            migrationBuilder.InsertData(
                schema: "leasing",
                table: "LandlordFieldConfigs",
                columns: cols,
                values: new[]
                {
                    Row("FullName", "Full Name", "Contact", "Primary Contact", 1, "text", true),
                    Row("Designation", "Designation", "Contact", "Primary Contact", 2, "text", false),
                    Row("Mobile", "Mobile Number", "Contact", "Primary Contact", 3, "phone", true),
                    Row("Email", "Email", "Contact", "Primary Contact", 4, "text", true),
                    Row("EmiratesId", "Emirates ID", "Contact", "Primary Contact", 5, "text", false),
                    Row("EmiratesIdName", "Name (as per Emirates ID)", "Contact", "Primary Contact", 6, "text", false),
                    Row("EmiratesIdExpiry", "Emirates ID Expiry", "Contact", "Primary Contact", 7, "date", false),
                    Row("CompanyName", "Company Name", "Landlord", "Company", 1, "text", false),
                });

            migrationBuilder.InsertData(
                schema: "leasing",
                table: "LandlordTabSettings",
                columns: new[] { "Section", "GroupName", "Title", "Sequence", "IsVisible" },
                values: new object[,]
                {
                    { "Contact", "Primary Contact", "Primary Contact", 1, true },
                    { "Landlord", "Company", "Company", 2, true },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "LandlordFieldConfigs", schema: "leasing");
            migrationBuilder.DropTable(name: "LandlordTabSettings", schema: "leasing");
        }
    }
}
