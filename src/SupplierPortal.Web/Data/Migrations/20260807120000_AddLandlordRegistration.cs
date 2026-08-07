using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLandlordRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LandlordRegistrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AgreementType = table.Column<int>(type: "int", nullable: false),
                    Plot = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PrimaryName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PrimaryMobile = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PrimaryEmail = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    HasSecondaryContact = table.Column<bool>(type: "bit", nullable: false),
                    SecondaryName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SecondaryMobile = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    SecondaryEmail = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    SigningRule = table.Column<int>(type: "int", nullable: false),
                    DeclarationAccepted = table.Column<bool>(type: "bit", nullable: false),
                    VerifiedName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    VerifiedEmail = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandlordRegistrations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LandlordSignatories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LandlordRegistrationId = table.Column<int>(type: "int", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Mobile = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandlordSignatories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LandlordSignatories_LandlordRegistrations_LandlordRegistrationId",
                        column: x => x.LandlordRegistrationId,
                        principalTable: "LandlordRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LandlordDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LandlordRegistrationId = table.Column<int>(type: "int", nullable: false),
                    Slot = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandlordDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LandlordDocuments_LandlordRegistrations_LandlordRegistrationId",
                        column: x => x.LandlordRegistrationId,
                        principalTable: "LandlordRegistrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LandlordDocuments_LandlordRegistrationId_Slot",
                table: "LandlordDocuments",
                columns: new[] { "LandlordRegistrationId", "Slot" });

            migrationBuilder.CreateIndex(
                name: "IX_LandlordRegistrations_ReferenceNumber",
                table: "LandlordRegistrations",
                column: "ReferenceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LandlordSignatories_LandlordRegistrationId",
                table: "LandlordSignatories",
                column: "LandlordRegistrationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LandlordDocuments");

            migrationBuilder.DropTable(
                name: "LandlordSignatories");

            migrationBuilder.DropTable(
                name: "LandlordRegistrations");
        }
    }
}
