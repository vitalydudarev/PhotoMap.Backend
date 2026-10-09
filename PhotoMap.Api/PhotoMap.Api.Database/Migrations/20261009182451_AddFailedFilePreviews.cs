using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoMap.Api.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddFailedFilePreviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "preview_attempted_at",
                table: "failed_files",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "preview_attempts",
                table: "failed_files",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "preview_content_type",
                table: "failed_files",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "preview_error",
                table: "failed_files",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "preview_file_path",
                table: "failed_files",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "preview_attempted_at",
                table: "failed_downloads",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "preview_attempts",
                table: "failed_downloads",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "preview_content_type",
                table: "failed_downloads",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "preview_error",
                table: "failed_downloads",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "preview_file_path",
                table: "failed_downloads",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "preview_attempted_at",
                table: "failed_files");

            migrationBuilder.DropColumn(
                name: "preview_attempts",
                table: "failed_files");

            migrationBuilder.DropColumn(
                name: "preview_content_type",
                table: "failed_files");

            migrationBuilder.DropColumn(
                name: "preview_error",
                table: "failed_files");

            migrationBuilder.DropColumn(
                name: "preview_file_path",
                table: "failed_files");

            migrationBuilder.DropColumn(
                name: "preview_attempted_at",
                table: "failed_downloads");

            migrationBuilder.DropColumn(
                name: "preview_attempts",
                table: "failed_downloads");

            migrationBuilder.DropColumn(
                name: "preview_content_type",
                table: "failed_downloads");

            migrationBuilder.DropColumn(
                name: "preview_error",
                table: "failed_downloads");

            migrationBuilder.DropColumn(
                name: "preview_file_path",
                table: "failed_downloads");
        }
    }
}
