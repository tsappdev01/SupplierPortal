using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierMasterData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BankBranchId",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BankId",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BuNameId",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CurrencyId",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentMethodId",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentTermsId",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PrefixId",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProvinceId",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VatCodeId",
                table: "Suppliers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneCode",
                table: "LookupValues",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Banks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Banks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BankBranches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BankId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankBranches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankBranches_Banks_BankId",
                        column: x => x.BankId,
                        principalTable: "Banks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_BankBranchId",
                table: "Suppliers",
                column: "BankBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_BankId",
                table: "Suppliers",
                column: "BankId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_BuNameId",
                table: "Suppliers",
                column: "BuNameId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_CurrencyId",
                table: "Suppliers",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_PaymentMethodId",
                table: "Suppliers",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_PaymentTermsId",
                table: "Suppliers",
                column: "PaymentTermsId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_PrefixId",
                table: "Suppliers",
                column: "PrefixId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_ProvinceId",
                table: "Suppliers",
                column: "ProvinceId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_VatCodeId",
                table: "Suppliers",
                column: "VatCodeId");

            migrationBuilder.CreateIndex(
                name: "IX_BankBranches_BankId",
                table: "BankBranches",
                column: "BankId");

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_BankBranches_BankBranchId",
                table: "Suppliers",
                column: "BankBranchId",
                principalTable: "BankBranches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_Banks_BankId",
                table: "Suppliers",
                column: "BankId",
                principalTable: "Banks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_LookupValues_BuNameId",
                table: "Suppliers",
                column: "BuNameId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_LookupValues_CurrencyId",
                table: "Suppliers",
                column: "CurrencyId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_LookupValues_PaymentMethodId",
                table: "Suppliers",
                column: "PaymentMethodId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_LookupValues_PaymentTermsId",
                table: "Suppliers",
                column: "PaymentTermsId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_LookupValues_PrefixId",
                table: "Suppliers",
                column: "PrefixId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_LookupValues_ProvinceId",
                table: "Suppliers",
                column: "ProvinceId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_LookupValues_VatCodeId",
                table: "Suppliers",
                column: "VatCodeId",
                principalTable: "LookupValues",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_BankBranches_BankBranchId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_Banks_BankId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_LookupValues_BuNameId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_LookupValues_CurrencyId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_LookupValues_PaymentMethodId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_LookupValues_PaymentTermsId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_LookupValues_PrefixId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_LookupValues_ProvinceId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_LookupValues_VatCodeId",
                table: "Suppliers");

            migrationBuilder.DropTable(
                name: "BankBranches");

            migrationBuilder.DropTable(
                name: "Banks");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_BankBranchId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_BankId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_BuNameId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_CurrencyId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_PaymentMethodId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_PaymentTermsId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_PrefixId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_ProvinceId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_VatCodeId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "BankBranchId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "BankId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "BuNameId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "CurrencyId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "PaymentMethodId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "PaymentTermsId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "PrefixId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "ProvinceId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "VatCodeId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "PhoneCode",
                table: "LookupValues");
        }
    }
}
