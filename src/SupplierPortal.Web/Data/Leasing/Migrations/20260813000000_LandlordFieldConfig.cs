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
            // NB: a rectangular object[,] is required here — a jagged object[][] binds to the single-row
            // InsertData overload and throws a values/columns count mismatch at startup. Column order below
            // must match the `columns` array exactly:
            // FieldKey, DisplayLabel, Section, GroupName, Sequence, FieldKind, BusinessControl, LandlordControl,
            // IsVisible, IsRequired, IsRemoved.
            migrationBuilder.InsertData(
                schema: "leasing",
                table: "LandlordFieldConfigs",
                columns: new[]
                {
                    "FieldKey", "DisplayLabel", "Section", "GroupName", "Sequence", "FieldKind",
                    "BusinessControl", "LandlordControl", "IsVisible", "IsRequired", "IsRemoved"
                },
                values: new object[,]
                {
                    { "FullName", "Full Name", "Contact", "Primary Contact", 1, "text", "Editable", "Editable", true, true, false },
                    { "Designation", "Designation", "Contact", "Primary Contact", 2, "text", "Editable", "Editable", true, false, false },
                    { "Mobile", "Mobile Number", "Contact", "Primary Contact", 3, "phone", "Editable", "Editable", true, true, false },
                    { "Email", "Email", "Contact", "Primary Contact", 4, "text", "Editable", "Editable", true, true, false },
                    { "EmiratesId", "Emirates ID", "Contact", "Primary Contact", 5, "text", "Editable", "Editable", true, false, false },
                    { "EmiratesIdName", "Name (as per Emirates ID)", "Contact", "Primary Contact", 6, "text", "Editable", "Editable", true, false, false },
                    { "EmiratesIdExpiry", "Emirates ID Expiry", "Contact", "Primary Contact", 7, "date", "Editable", "Editable", true, false, false },
                    { "CompanyName", "Company Name", "Landlord", "Company", 1, "text", "Editable", "Editable", true, false, false },
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
