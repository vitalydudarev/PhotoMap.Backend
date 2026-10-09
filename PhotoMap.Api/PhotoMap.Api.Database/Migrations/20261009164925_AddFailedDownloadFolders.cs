using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoMap.Api.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddFailedDownloadFolders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_failed_downloads_user_id_photo_source_id_file_name",
                table: "failed_downloads");

            migrationBuilder.AddColumn<string>(
                name: "folder",
                table: "failed_downloads",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "kind",
                table: "failed_downloads",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "ix_failed_downloads_user_id_photo_source_id_kind_folder_file_n",
                table: "failed_downloads",
                columns: new[] { "user_id", "photo_source_id", "kind", "folder", "file_name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_failed_downloads_user_id_photo_source_id_kind_folder_file_n",
                table: "failed_downloads");

            migrationBuilder.DropColumn(
                name: "folder",
                table: "failed_downloads");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "failed_downloads");

            migrationBuilder.CreateIndex(
                name: "ix_failed_downloads_user_id_photo_source_id_file_name",
                table: "failed_downloads",
                columns: new[] { "user_id", "photo_source_id", "file_name" },
                unique: true);
        }
    }
}
