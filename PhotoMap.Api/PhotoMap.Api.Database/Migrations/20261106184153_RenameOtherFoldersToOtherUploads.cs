using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoMap.Api.Database.Migrations
{
    /// <summary>
    /// Renames the other folders to the other uploads. The table, its keys and index, and the column are renamed
    /// rather than dropped and created again, so that the statuses and the photos keep their data.
    /// </summary>
    public partial class RenameOtherFoldersToOtherUploads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "from_other_folder",
                table: "photos",
                newName: "from_other_uploads");

            migrationBuilder.RenameTable(
                name: "users_other_folders_status",
                newName: "users_other_uploads_status");

            migrationBuilder.Sql(
                "ALTER TABLE users_other_uploads_status RENAME CONSTRAINT pk_users_other_folders_status TO pk_users_other_uploads_status;");
            migrationBuilder.Sql(
                "ALTER TABLE users_other_uploads_status RENAME CONSTRAINT fk_users_other_folders_status_photo_sources_photo_source_id TO fk_users_other_uploads_status_photo_sources_photo_source_id;");
            migrationBuilder.Sql(
                "ALTER TABLE users_other_uploads_status RENAME CONSTRAINT fk_users_other_folders_status_users_user_id TO fk_users_other_uploads_status_users_user_id;");

            migrationBuilder.RenameIndex(
                name: "ix_users_other_folders_status_photo_source_id",
                table: "users_other_uploads_status",
                newName: "ix_users_other_uploads_status_photo_source_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "from_other_uploads",
                table: "photos",
                newName: "from_other_folder");

            migrationBuilder.RenameTable(
                name: "users_other_uploads_status",
                newName: "users_other_folders_status");

            migrationBuilder.Sql(
                "ALTER TABLE users_other_folders_status RENAME CONSTRAINT pk_users_other_uploads_status TO pk_users_other_folders_status;");
            migrationBuilder.Sql(
                "ALTER TABLE users_other_folders_status RENAME CONSTRAINT fk_users_other_uploads_status_photo_sources_photo_source_id TO fk_users_other_folders_status_photo_sources_photo_source_id;");
            migrationBuilder.Sql(
                "ALTER TABLE users_other_folders_status RENAME CONSTRAINT fk_users_other_uploads_status_users_user_id TO fk_users_other_folders_status_users_user_id;");

            migrationBuilder.RenameIndex(
                name: "ix_users_other_uploads_status_photo_source_id",
                table: "users_other_folders_status",
                newName: "ix_users_other_folders_status_photo_source_id");
        }
    }
}
