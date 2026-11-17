using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoMap.Api.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoExif : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "exif_string",
                table: "videos",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "exif_string",
                table: "videos");
        }
    }
}
