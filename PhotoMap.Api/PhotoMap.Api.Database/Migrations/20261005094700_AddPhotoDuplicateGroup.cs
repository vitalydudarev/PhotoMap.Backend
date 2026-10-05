using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoMap.Api.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoDuplicateGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "duplicate_group_id",
                table: "photos",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_photos_user_id_duplicate_group_id",
                table: "photos",
                columns: new[] { "user_id", "duplicate_group_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_photos_user_id_duplicate_group_id",
                table: "photos");

            migrationBuilder.DropColumn(
                name: "duplicate_group_id",
                table: "photos");
        }
    }
}
