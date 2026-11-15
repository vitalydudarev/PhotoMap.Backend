using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoMap.Api.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoGeocoding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "geocoding",
                table: "photos",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_photos_not_geocoded",
                table: "photos",
                column: "id",
                filter: "has_gps AND geocoding IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_photos_not_geocoded",
                table: "photos");

            migrationBuilder.DropColumn(
                name: "geocoding",
                table: "photos");
        }
    }
}
