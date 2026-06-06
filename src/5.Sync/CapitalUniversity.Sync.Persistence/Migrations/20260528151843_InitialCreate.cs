using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalUniversity.Sync.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "sync");

            migrationBuilder.CreateTable(
                name: "checkpoints",
                schema: "sync",
                columns: table => new
                {
                    ModuleName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Cursor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastRowVersion = table.Column<long>(type: "bigint", nullable: true),
                    LastExternalVersion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_checkpoints", x => x.ModuleName);
                });

            migrationBuilder.CreateTable(
                name: "dead_letters",
                schema: "sync",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HangfireJobId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ModuleName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    AttemptedCount = table.Column<int>(type: "int", nullable: false),
                    TerminalAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dead_letters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "failures",
                schema: "sync",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HangfireJobId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Attempt = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ErrorType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    StackTrace = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_failures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "jobs",
                schema: "sync",
                columns: table => new
                {
                    HangfireJobId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModuleName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    Queue = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    EnqueuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jobs", x => x.HangfireJobId);
                });

            migrationBuilder.CreateTable(
                name: "runs",
                schema: "sync",
                columns: table => new
                {
                    CorrelationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModuleName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    TriggeredBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Queue = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    EnqueuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RecordsProcessed = table.Column<int>(type: "int", nullable: true),
                    RecordsFailed = table.Column<int>(type: "int", nullable: true),
                    DurationTicks = table.Column<long>(type: "bigint", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_runs", x => x.CorrelationId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dead_letters_CorrelationId",
                schema: "sync",
                table: "dead_letters",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_dead_letters_TerminalAt",
                schema: "sync",
                table: "dead_letters",
                column: "TerminalAt");

            migrationBuilder.CreateIndex(
                name: "IX_failures_CorrelationId",
                schema: "sync",
                table: "failures",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_failures_OccurredAt",
                schema: "sync",
                table: "failures",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_jobs_CorrelationId",
                schema: "sync",
                table: "jobs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_runs_ModuleName_EnqueuedAt",
                schema: "sync",
                table: "runs",
                columns: new[] { "ModuleName", "EnqueuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_runs_Status",
                schema: "sync",
                table: "runs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "checkpoints",
                schema: "sync");

            migrationBuilder.DropTable(
                name: "dead_letters",
                schema: "sync");

            migrationBuilder.DropTable(
                name: "failures",
                schema: "sync");

            migrationBuilder.DropTable(
                name: "jobs",
                schema: "sync");

            migrationBuilder.DropTable(
                name: "runs",
                schema: "sync");
        }
    }
}