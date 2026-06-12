using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalUniversity.Module.StudentServices.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SyncModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TransitionType",
                schema: "StudentServices",
                table: "WorkflowSteps",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                schema: "StudentServices",
                table: "StudentRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsClosed",
                schema: "StudentServices",
                table: "StudentRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TransitionType",
                schema: "StudentServices",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                schema: "StudentServices",
                table: "StudentRequests");

            migrationBuilder.DropColumn(
                name: "IsClosed",
                schema: "StudentServices",
                table: "StudentRequests");
        }
    }
}
