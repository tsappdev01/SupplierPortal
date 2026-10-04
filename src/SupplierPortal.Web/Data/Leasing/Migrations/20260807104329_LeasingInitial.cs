using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupplierPortal.Web.Data.Leasing.Migrations
{
    /// <inheritdoc />
    public partial class LeasingInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "leasing");

            migrationBuilder.CreateTable(
                name: "LandlordEvents",
                schema: "leasing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LandlordId = table.Column<int>(type: "int", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    At = table.Column<DateTime>(type: "datetime2", nullable: false),
                    By = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandlordEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LandlordFieldChanges",
                schema: "leasing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LandlordId = table.Column<int>(type: "int", nullable: false),
                    LandlordAccessTokenId = table.Column<int>(type: "int", nullable: true),
                    FieldKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    DisplayLabel = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChangedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsAcknowledged = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandlordFieldChanges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Landlords",
                schema: "leasing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BusinessUnitId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    LegalName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    PrimaryContactName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Mobile = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    EmiratesId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TradeLicenseNo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    TaxRegistrationNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    SecondaryContactName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    SecondaryContactEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SecondaryContactMobile = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    SigningRule = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Landlords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LandlordAccessTokens",
                schema: "leasing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LandlordId = table.Column<int>(type: "int", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OtpCodeHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OtpGeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OtpExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OtpAttemptCount = table.Column<int>(type: "int", nullable: false),
                    OtpSendCount = table.Column<int>(type: "int", nullable: false),
                    OtpVerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SessionTokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SessionExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandlordAccessTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LandlordAccessTokens_Landlords_LandlordId",
                        column: x => x.LandlordId,
                        principalSchema: "leasing",
                        principalTable: "Landlords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LandlordDocuments",
                schema: "leasing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LandlordId = table.Column<int>(type: "int", nullable: false),
                    DocumentType = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SignatoryId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandlordDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LandlordDocuments_Landlords_LandlordId",
                        column: x => x.LandlordId,
                        principalSchema: "leasing",
                        principalTable: "Landlords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LandlordPremises",
                schema: "leasing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LandlordId = table.Column<int>(type: "int", nullable: false),
                    PremisesId = table.Column<int>(type: "int", nullable: false),
                    PropertyCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    BuildingName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Zone = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    LinkedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LinkedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandlordPremises", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LandlordPremises_Landlords_LandlordId",
                        column: x => x.LandlordId,
                        principalSchema: "leasing",
                        principalTable: "Landlords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LandlordSignatories",
                schema: "leasing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LandlordId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Mobile = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Order = table.Column<int>(type: "int", nullable: false),
                    IsPrimaryContact = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandlordSignatories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LandlordSignatories_Landlords_LandlordId",
                        column: x => x.LandlordId,
                        principalSchema: "leasing",
                        principalTable: "Landlords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LandlordAccessTokens_LandlordId",
                schema: "leasing",
                table: "LandlordAccessTokens",
                column: "LandlordId");

            migrationBuilder.CreateIndex(
                name: "IX_LandlordAccessTokens_TokenHash",
                schema: "leasing",
                table: "LandlordAccessTokens",
                column: "TokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_LandlordDocuments_LandlordId",
                schema: "leasing",
                table: "LandlordDocuments",
                column: "LandlordId");

            migrationBuilder.CreateIndex(
                name: "IX_LandlordEvents_LandlordId",
                schema: "leasing",
                table: "LandlordEvents",
                column: "LandlordId");

            migrationBuilder.CreateIndex(
                name: "IX_LandlordFieldChanges_LandlordId_IsAcknowledged",
                schema: "leasing",
                table: "LandlordFieldChanges",
                columns: new[] { "LandlordId", "IsAcknowledged" });

            migrationBuilder.CreateIndex(
                name: "IX_LandlordPremises_LandlordId_PremisesId",
                schema: "leasing",
                table: "LandlordPremises",
                columns: new[] { "LandlordId", "PremisesId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LandlordPremises_PremisesId",
                schema: "leasing",
                table: "LandlordPremises",
                column: "PremisesId");

            migrationBuilder.CreateIndex(
                name: "IX_Landlords_BusinessUnitId",
                schema: "leasing",
                table: "Landlords",
                column: "BusinessUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Landlords_Status",
                schema: "leasing",
                table: "Landlords",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LandlordSignatories_LandlordId",
                schema: "leasing",
                table: "LandlordSignatories",
                column: "LandlordId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LandlordAccessTokens",
                schema: "leasing");

            migrationBuilder.DropTable(
                name: "LandlordDocuments",
                schema: "leasing");

            migrationBuilder.DropTable(
                name: "LandlordEvents",
                schema: "leasing");

            migrationBuilder.DropTable(
                name: "LandlordFieldChanges",
                schema: "leasing");

            migrationBuilder.DropTable(
                name: "LandlordPremises",
                schema: "leasing");

            migrationBuilder.DropTable(
                name: "LandlordSignatories",
                schema: "leasing");

            migrationBuilder.DropTable(
                name: "Landlords",
                schema: "leasing");
        }
    }
}
