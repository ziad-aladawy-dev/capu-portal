using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalUniversity.Sync.Finance.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "sync_finance");

            migrationBuilder.CreateTable(
                name: "invoices_outbox",
                schema: "sync_finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalInvoiceId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Operation = table.Column<int>(type: "int", nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PayloadSchemaVersion = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoices_outbox", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_invoices_outbox_Status_CreatedAt",
                schema: "sync_finance",
                table: "invoices_outbox",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "invoices_outbox",
                schema: "sync_finance");
        }
    }
}
