using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalUniversity.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// FIX (2026-05-25) — this migration was a full duplicate of two earlier
    /// migrations and is now an intentional no-op:
    /// <list type="bullet">
    ///   <item><description>Its <c>AddColumn&lt;int&gt;(name: "SessionVersion", ...)</c>
    ///   calls duplicated <see cref="AddSessionVersion"/> (20260517000001).</description></item>
    ///   <item><description>Its <c>CreateTable("OutboxMessages")</c> duplicated
    ///   <see cref="AddOutboxMessages"/> (20260517000002).</description></item>
    /// </list>
    /// On a fresh database, running r2 after those two raised
    /// "Column name 'SessionVersion' in table 'Students' is specified more
    /// than once" / "There is already an object named 'OutboxMessages'".
    ///
    /// <para>Deletion-vs-no-op:</para> we keep the (empty) migration class so
    /// any dev whose <c>__EFMigrationsHistory</c> already contains the
    /// <c>20260518154708_r2</c> row still has a matching type in the assembly
    /// (EF would otherwise refuse to start). The empty Up/Down do nothing on
    /// a future re-apply; their state is owned by AddSessionVersion +
    /// AddOutboxMessages, which are the canonical sources.
    /// </remarks>
    public partial class r2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty. See class remarks.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty. See class remarks.
        }
    }
}
