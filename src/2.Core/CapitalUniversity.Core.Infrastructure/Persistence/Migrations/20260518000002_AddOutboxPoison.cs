using System;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalUniversity.Core.Infrastructure.Persistence.Migrations
{
    // Hand-shipped (same pattern as the earlier infra migrations) — no
    // .Designer.cs sidecar. Adds the poison-queue columns + an operator
    // index for "show me everything stuck right now".
    [DbContext(typeof(CoreDbContext))]
    [Migration("20260518000002_AddOutboxPoison")]
    public partial class AddOutboxPoison : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPoisoned",
                table: "OutboxMessages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PoisonedAt",
                table: "OutboxMessages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_IsPoisoned_PoisonedAt",
                table: "OutboxMessages",
                columns: new[] { "IsPoisoned", "PoisonedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_IsPoisoned_PoisonedAt",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(name: "PoisonedAt", table: "OutboxMessages");
            migrationBuilder.DropColumn(name: "IsPoisoned", table: "OutboxMessages");
        }
    }
}
