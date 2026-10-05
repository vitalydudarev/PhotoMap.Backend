using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoMap.Api.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoDuplicateGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "duplicate_group_id",
                table: "videos",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_videos_user_id_duplicate_group_id",
                table: "videos",
                columns: new[] { "user_id", "duplicate_group_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_videos_user_id_duplicate_group_id",
                table: "videos");

            migrationBuilder.DropColumn(
                name: "duplicate_group_id",
                table: "videos");
        }
    }
}
