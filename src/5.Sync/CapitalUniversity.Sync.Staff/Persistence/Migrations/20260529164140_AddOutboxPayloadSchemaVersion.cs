using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalUniversity.Sync.Staff.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxPayloadSchemaVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Default = 1 (the current schema version): pre-existing rows are treated as
            // already-on-version-1 — the introductory version. Future bumps update both
            // the entity's CurrentPayloadSchemaVersion constant and this migration default.
            migrationBuilder.AddColumn<int>(
                name: "PayloadSchemaVersion",
                schema: "sync_staff",
                table: "staff_outbox",
                type: "int",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PayloadSchemaVersion",
                schema: "sync_staff",
                table: "staff_outbox");
        }
    }
}