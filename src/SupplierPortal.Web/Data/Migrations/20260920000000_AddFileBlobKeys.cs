using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupplierPortal.Web.Data;

#nullable disable

namespace SupplierPortal.Web.Data.Migrations
{
    /// <summary>Adds blob-storage key columns for user-uploaded files (supplier &amp; customer documents,
    /// profile photos, business-unit logos) so new uploads can live in Azure Blob Storage while existing
    /// files keep working from disk / the database.</summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260920000000_AddFileBlobKeys")]
    public partial class AddFileBlobKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BlobKey", table: "SupplierDocuments", type: "nvarchar(400)", maxLength: 400, nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BlobKey", table: "CustomerDocuments", type: "nvarchar(400)", maxLength: 400, nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfilePhotoBlobKey", table: "Users", type: "nvarchar(400)", maxLength: 400, nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoBlobKey", table: "BusinessUnits", type: "nvarchar(400)", maxLength: 400, nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailBlobKey", table: "BusinessUnits", type: "nvarchar(400)", maxLength: 400, nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "BlobKey", table: "SupplierDocuments");
            migrationBuilder.DropColumn(name: "BlobKey", table: "CustomerDocuments");
            migrationBuilder.DropColumn(name: "ProfilePhotoBlobKey", table: "Users");
            migrationBuilder.DropColumn(name: "LogoBlobKey", table: "BusinessUnits");
            migrationBuilder.DropColumn(name: "ThumbnailBlobKey", table: "BusinessUnits");
        }
    }
}
