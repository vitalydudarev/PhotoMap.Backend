using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoMap.Api.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "categories_version",
                table: "photos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "photo_categories",
                columns: table => new
                {
                    photo_id = table.Column<long>(type: "bigint", nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_photo_categories", x => new { x.photo_id, x.category });
                    table.ForeignKey(
                        name: "fk_photo_categories_photos_photo_id",
                        column: x => x.photo_id,
                        principalTable: "photos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_photos_categories_version",
                table: "photos",
                column: "categories_version");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "photo_categories");

            migrationBuilder.DropIndex(
                name: "ix_photos_categories_version",
                table: "photos");

            migrationBuilder.DropColumn(
                name: "categories_version",
                table: "photos");
        }
    }
}
