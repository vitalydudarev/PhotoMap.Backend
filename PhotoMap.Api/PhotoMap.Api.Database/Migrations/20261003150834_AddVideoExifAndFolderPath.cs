using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoMap.Api.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoExifAndFolderPath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "path",
                table: "videos",
                newName: "folder_path");

            // the videos saved so far keep their full path, the folder is what is left without the file name;
            // a file at the root of the disk (disk:/video.mp4) is in disk:/
            migrationBuilder.Sql("""
                UPDATE videos
                SET folder_path = CASE
                    WHEN folder_path ~ '^[^/]*:/[^/]*$' THEN regexp_replace(folder_path, '[^/]*$', '')
                    ELSE regexp_replace(folder_path, '/[^/]*$', '')
                END
                WHERE folder_path LIKE '%/%';
                """);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "exif_date_time",
                table: "videos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "latitude",
                table: "videos",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "longitude",
                table: "videos",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "exif_date_time",
                table: "videos");

            migrationBuilder.DropColumn(
                name: "latitude",
                table: "videos");

            migrationBuilder.DropColumn(
                name: "longitude",
                table: "videos");

            migrationBuilder.Sql("""
                UPDATE videos
                SET folder_path = CASE
                    WHEN folder_path LIKE '%/' THEN folder_path || file_name
                    ELSE folder_path || '/' || file_name
                END
                WHERE folder_path IS NOT NULL;
                """);

            migrationBuilder.RenameColumn(
                name: "folder_path",
                table: "videos",
                newName: "path");
        }
    }
}
