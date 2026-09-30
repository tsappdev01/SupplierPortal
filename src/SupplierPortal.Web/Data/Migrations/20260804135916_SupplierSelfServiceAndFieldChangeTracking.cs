using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class SupplierSelfServiceAndFieldChangeTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupplierAccessTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsAdminTest = table.Column<bool>(type: "bit", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OtpCodeHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
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
                    table.PrimaryKey("PK_SupplierAccessTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierAccessTokens_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    DocumentType = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierDocuments_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierFieldChanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    SupplierAccessTokenId = table.Column<int>(type: "int", nullable: true),
                    FieldKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayLabel = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChangedBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    IsAcknowledged = table.Column<bool>(type: "bit", nullable: false),
                    AcknowledgedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcknowledgedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierFieldChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierFieldChanges_SupplierAccessTokens_SupplierAccessTokenId",
                        column: x => x.SupplierAccessTokenId,
                        principalTable: "SupplierAccessTokens",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SupplierFieldChanges_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierAccessTokens_SupplierId",
                table: "SupplierAccessTokens",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierAccessTokens_TokenHash",
                table: "SupplierAccessTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierDocuments_SupplierId_DocumentType",
                table: "SupplierDocuments",
                columns: new[] { "SupplierId", "DocumentType" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierFieldChanges_SupplierAccessTokenId",
                table: "SupplierFieldChanges",
                column: "SupplierAccessTokenId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierFieldChanges_SupplierId_IsAcknowledged",
                table: "SupplierFieldChanges",
                columns: new[] { "SupplierId", "IsAcknowledged" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplierDocuments");

            migrationBuilder.DropTable(
                name: "SupplierFieldChanges");

            migrationBuilder.DropTable(
                name: "SupplierAccessTokens");
        }
    }
}
