using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoMap.Api.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoFolderPath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "folder_path",
                table: "photos",
                type: "text",
                nullable: true,
                computedColumnSql: "CASE WHEN strpos(path, '/') = 0 THEN NULL ELSE regexp_replace(regexp_replace(path, '/[^/]*$', ''), '(^|:)$', '\\1/') END",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "ix_photos_user_id_folder_path",
                table: "photos",
                columns: new[] { "user_id", "folder_path" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_photos_user_id_folder_path",
                table: "photos");

            migrationBuilder.DropColumn(
                name: "folder_path",
                table: "photos");
        }
    }
}
