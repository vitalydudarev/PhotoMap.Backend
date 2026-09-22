using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoMap.Api.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoExternalIdAndContentHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_photos_user_id",
                table: "photos");

            migrationBuilder.AddColumn<string>(
                name: "content_hash",
                table: "photos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "external_id",
                table: "photos",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_photos_user_id_content_hash",
                table: "photos",
                columns: new[] { "user_id", "content_hash" });

            migrationBuilder.CreateIndex(
                name: "ix_photos_user_id_photo_source_id_external_id",
                table: "photos",
                columns: new[] { "user_id", "photo_source_id", "external_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_photos_user_id_content_hash",
                table: "photos");

            migrationBuilder.DropIndex(
                name: "ix_photos_user_id_photo_source_id_external_id",
                table: "photos");

            migrationBuilder.DropColumn(
                name: "content_hash",
                table: "photos");

            migrationBuilder.DropColumn(
                name: "external_id",
                table: "photos");

            migrationBuilder.CreateIndex(
                name: "ix_photos_user_id",
                table: "photos",
                column: "user_id");
        }
    }
}
