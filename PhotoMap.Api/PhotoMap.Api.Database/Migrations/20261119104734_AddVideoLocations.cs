using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoMap.Api.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "geocoding",
                table: "videos",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "video_locations",
                columns: table => new
                {
                    video_id = table.Column<long>(type: "bigint", nullable: false),
                    location_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_video_locations", x => new { x.video_id, x.location_id });
                    table.ForeignKey(
                        name: "fk_video_locations_locations_location_id",
                        column: x => x.location_id,
                        principalTable: "locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_video_locations_videos_video_id",
                        column: x => x.video_id,
                        principalTable: "videos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_video_locations_location_id",
                table: "video_locations",
                column: "location_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "video_locations");

            migrationBuilder.DropColumn(
                name: "geocoding",
                table: "videos");
        }
    }
}
