using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalUniversity.Sync.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexOnDeadLetterHangfireJobId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // One dead-letter row per Hangfire job.
            //
            // Operator note: if the deployment target may already contain duplicate
            // HangfireJobId rows (the pre-constraint race window), the index
            // creation will fail with msg 1505. Resolve before re-running by
            // collapsing duplicates, e.g.:
            //
            //   ;WITH duplicates AS (
            //       SELECT Id,
            //              ROW_NUMBER() OVER (
            //                  PARTITION BY HangfireJobId
            //                  ORDER BY TerminalAt DESC, Id DESC) AS rn
            //       FROM sync.dead_letters)
            //   DELETE FROM duplicates WHERE rn > 1;
            //
            // Staging environments should run the SELECT form first to confirm
            // the duplicate count.
            migrationBuilder.CreateIndex(
                name: "IX_dead_letters_HangfireJobId",
                schema: "sync",
                table: "dead_letters",
                column: "HangfireJobId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_dead_letters_HangfireJobId",
                schema: "sync",
                table: "dead_letters");
        }
    }
}
