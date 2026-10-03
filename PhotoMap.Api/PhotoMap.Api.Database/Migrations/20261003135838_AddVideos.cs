using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PhotoMap.Api.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddVideos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users_video_sources_status",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    photo_source_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    total_count = table.Column<int>(type: "integer", nullable: false),
                    processed_count = table.Column<int>(type: "integer", nullable: false),
                    failed_count = table.Column<int>(type: "integer", nullable: false),
                    last_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users_video_sources_status", x => new { x.user_id, x.photo_source_id });
                    table.ForeignKey(
                        name: "fk_users_video_sources_status_photo_sources_photo_source_id",
                        column: x => x.photo_source_id,
                        principalTable: "photo_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_users_video_sources_status_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "videos",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    photo_source_id = table.Column<long>(type: "bigint", nullable: false),
                    external_id = table.Column<string>(type: "text", nullable: false),
                    file_name = table.Column<string>(type: "text", nullable: false),
                    path = table.Column<string>(type: "text", nullable: true),
                    mime_type = table.Column<string>(type: "text", nullable: true),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    date_time_taken = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    preview_file_path = table.Column<string>(type: "text", nullable: true),
                    preview_content_type = table.Column<string>(type: "text", nullable: true),
                    added_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_videos", x => x.id);
                    table.ForeignKey(
                        name: "fk_videos_photo_sources_photo_source_id",
                        column: x => x.photo_source_id,
                        principalTable: "photo_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_videos_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_users_video_sources_status_photo_source_id",
                table: "users_video_sources_status",
                column: "photo_source_id");

            migrationBuilder.CreateIndex(
                name: "ix_videos_photo_source_id",
                table: "videos",
                column: "photo_source_id");

            migrationBuilder.CreateIndex(
                name: "ix_videos_user_id_date_time_taken",
                table: "videos",
                columns: new[] { "user_id", "date_time_taken" });

            migrationBuilder.CreateIndex(
                name: "ix_videos_user_id_photo_source_id_external_id",
                table: "videos",
                columns: new[] { "user_id", "photo_source_id", "external_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "users_video_sources_status");

            migrationBuilder.DropTable(
                name: "videos");
        }
    }
}
