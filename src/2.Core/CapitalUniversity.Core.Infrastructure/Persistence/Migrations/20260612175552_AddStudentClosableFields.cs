using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalUniversity.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentClosableFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "Students",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsClosed",
                table: "Students",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsClosed",
                table: "Orders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "StudentFees",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsClosed",
                table: "StudentFees",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "StudentFees");

            migrationBuilder.DropColumn(
                name: "IsClosed",
                table: "StudentFees");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "IsClosed",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "IsClosed",
                table: "Students");
        }
    }
}
