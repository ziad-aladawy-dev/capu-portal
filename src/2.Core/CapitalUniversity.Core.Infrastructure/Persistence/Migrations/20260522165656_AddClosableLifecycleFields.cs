using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalUniversity.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClosableLifecycleFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "ScheduleSlots",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsClosed",
                table: "ScheduleSlots",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "Invoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsClosed",
                table: "Invoices",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "Courses",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsClosed",
                table: "Courses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "CourseOfferings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsClosed",
                table: "CourseOfferings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "AcademicPlans",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsClosed",
                table: "AcademicPlans",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "ScheduleSlots");

            migrationBuilder.DropColumn(
                name: "IsClosed",
                table: "ScheduleSlots");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "IsClosed",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "IsClosed",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "CourseOfferings");

            migrationBuilder.DropColumn(
                name: "IsClosed",
                table: "CourseOfferings");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "AcademicPlans");

            migrationBuilder.DropColumn(
                name: "IsClosed",
                table: "AcademicPlans");
        }
    }
}
