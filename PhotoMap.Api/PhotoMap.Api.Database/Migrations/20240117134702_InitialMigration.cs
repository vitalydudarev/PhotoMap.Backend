using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using PhotoMap.Api.Domain.Models;

#nullable disable

namespace PhotoMap.Api.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "photo_sources",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    settings = table.Column<string>(type: "text", nullable: false),
                    client_auth_settings = table.Column<ClientAuthSettings>(type: "jsonb", nullable: false),
                    service_factory_implementation_type = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_photo_sources", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "photos",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    photo_source_id = table.Column<long>(type: "bigint", nullable: false),
                    thumbnail_small_file_path = table.Column<string>(type: "text", nullable: true),
                    thumbnail_large_file_path = table.Column<string>(type: "text", nullable: true),
                    file_name = table.Column<string>(type: "text", nullable: false),
                    date_time_taken = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    has_gps = table.Column<bool>(type: "boolean", nullable: false),
                    exif_string = table.Column<string>(type: "text", nullable: true),
                    path = table.Column<string>(type: "text", nullable: true),
                    added_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_photos", x => x.id);
                    table.ForeignKey(
                        name: "fk_photos_photo_sources_photo_source_id",
                        column: x => x.photo_source_id,
                        principalTable: "photo_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_photos_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "users_photo_sources",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    photo_source_id = table.Column<long>(type: "bigint", nullable: false),
                    user_auth_result = table.Column<UserAuthResult>(type: "jsonb", nullable: true),
                    processing_state = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users_photo_sources", x => new { x.user_id, x.photo_source_id });
                    table.ForeignKey(
                        name: "fk_users_photo_sources_photo_sources_photo_source_id",
                        column: x => x.photo_source_id,
                        principalTable: "photo_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_users_photo_sources_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "users_photo_sources_status",
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
                    table.PrimaryKey("pk_users_photo_sources_status", x => new { x.user_id, x.photo_source_id });
                    table.ForeignKey(
                        name: "fk_users_photo_sources_status_photo_sources_photo_source_id",
                        column: x => x.photo_source_id,
                        principalTable: "photo_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_users_photo_sources_status_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_photos_photo_source_id",
                table: "photos",
                column: "photo_source_id");

            migrationBuilder.CreateIndex(
                name: "ix_photos_user_id",
                table: "photos",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_photo_sources_photo_source_id",
                table: "users_photo_sources",
                column: "photo_source_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_photo_sources_status_photo_source_id",
                table: "users_photo_sources_status",
                column: "photo_source_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "photos");

            migrationBuilder.DropTable(
                name: "users_photo_sources");

            migrationBuilder.DropTable(
                name: "users_photo_sources_status");

            migrationBuilder.DropTable(
                name: "photo_sources");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
