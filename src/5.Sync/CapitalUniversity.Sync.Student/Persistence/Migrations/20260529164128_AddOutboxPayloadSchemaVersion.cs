using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalUniversity.Sync.Student.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxPayloadSchemaVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Default = 1 (the current schema version): pre-existing rows written before
            // this column existed are treated as already-on-version-1 — the introductory
            // version. Future schema bumps will set the default to the new version and the
            // mapper's version check will fail on stale rows.
            migrationBuilder.AddColumn<int>(
                name: "PayloadSchemaVersion",
                schema: "sync_student",
                table: "student_outbox",
                type: "int",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PayloadSchemaVersion",
                schema: "sync_student",
                table: "student_outbox");
        }
    }
}