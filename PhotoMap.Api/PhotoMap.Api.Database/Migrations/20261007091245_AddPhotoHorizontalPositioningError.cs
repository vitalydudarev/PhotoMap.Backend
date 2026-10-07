using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoMap.Api.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoHorizontalPositioningError : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "horizontal_positioning_error",
                table: "photos",
                type: "double precision",
                nullable: true);

            // the photos saved so far have it in their EXIF, as the worker serialized it
            migrationBuilder.Sql("""
                UPDATE photos
                SET horizontal_positioning_error = (exif_string::jsonb -> 'Gps' ->> 'HorizontalPositioningError')::double precision
                WHERE exif_string LIKE '%"HorizontalPositioningError":%'
                    AND jsonb_typeof(exif_string::jsonb -> 'Gps' -> 'HorizontalPositioningError') = 'number';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "horizontal_positioning_error",
                table: "photos");
        }
    }
}
