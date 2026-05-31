using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalUniversity.Sync.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHangfireJobIdToRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HangfireJobId",
                schema: "sync",
                table: "runs",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_runs_orphan",
                schema: "sync",
                table: "runs",
                columns: new[] { "Status", "HangfireJobId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_runs_orphan",
                schema: "sync",
                table: "runs");

            migrationBuilder.DropColumn(
                name: "HangfireJobId",
                schema: "sync",
                table: "runs");
        }
    }
}