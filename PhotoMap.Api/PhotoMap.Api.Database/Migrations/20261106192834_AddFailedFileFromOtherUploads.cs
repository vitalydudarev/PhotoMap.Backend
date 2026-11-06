using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoMap.Api.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddFailedFileFromOtherUploads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "from_other_uploads",
                table: "failed_files",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "from_other_uploads",
                table: "failed_files");
        }
    }
}
