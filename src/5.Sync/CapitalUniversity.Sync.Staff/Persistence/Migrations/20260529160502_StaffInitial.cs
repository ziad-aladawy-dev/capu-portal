using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalUniversity.Sync.Staff.Migrations
{
    /// <inheritdoc />
    public partial class StaffInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "sync_staff");

            migrationBuilder.CreateTable(
                name: "staff",
                schema: "sync_staff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalStaffId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Department = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExternalUpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExternalVersion = table.Column<int>(type: "int", nullable: false),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OriginSystem = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "staff_outbox",
                schema: "sync_staff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalStaffId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Operation = table.Column<int>(type: "int", nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_outbox", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_staff_ExternalStaffId",
                schema: "sync_staff",
                table: "staff",
                column: "ExternalStaffId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_staff_ExternalUpdatedAt",
                schema: "sync_staff",
                table: "staff",
                column: "ExternalUpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_staff_outbox_Status_CreatedAt",
                schema: "sync_staff",
                table: "staff_outbox",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "staff",
                schema: "sync_staff");

            migrationBuilder.DropTable(
                name: "staff_outbox",
                schema: "sync_staff");
        }
    }
}