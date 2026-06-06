using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalUniversity.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExternallySourced : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ExternalSystemId",
                table: "CourseOfferings",
                newName: "ExternalId");

            migrationBuilder.RenameColumn(
                name: "ExternalSyncedAt",
                table: "CourseOfferings",
                newName: "LastSyncedAt");

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Students",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExternalUpdatedAt",
                table: "Students",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExternalVersion",
                table: "Students",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncedAt",
                table: "Students",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginSystem",
                table: "Students",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Staffs",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExternalUpdatedAt",
                table: "Staffs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExternalVersion",
                table: "Staffs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncedAt",
                table: "Staffs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginSystem",
                table: "Staffs",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "ScheduleSlots",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExternalUpdatedAt",
                table: "ScheduleSlots",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExternalVersion",
                table: "ScheduleSlots",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncedAt",
                table: "ScheduleSlots",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginSystem",
                table: "ScheduleSlots",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Invoices",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExternalUpdatedAt",
                table: "Invoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExternalVersion",
                table: "Invoices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncedAt",
                table: "Invoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginSystem",
                table: "Invoices",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Courses",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExternalUpdatedAt",
                table: "Courses",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExternalVersion",
                table: "Courses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncedAt",
                table: "Courses",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginSystem",
                table: "Courses",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "ExternalUpdatedAt",
                table: "CourseOfferings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExternalVersion",
                table: "CourseOfferings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginSystem",
                table: "CourseOfferings",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Students_ExternalId",
                table: "Students",
                column: "ExternalId",
                unique: true,
                filter: "[ExternalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Staffs_ExternalId",
                table: "Staffs",
                column: "ExternalId",
                unique: true,
                filter: "[ExternalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleSlots_ExternalId",
                table: "ScheduleSlots",
                column: "ExternalId",
                unique: true,
                filter: "[ExternalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ExternalId",
                table: "Invoices",
                column: "ExternalId",
                unique: true,
                filter: "[ExternalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Courses_ExternalId",
                table: "Courses",
                column: "ExternalId",
                unique: true,
                filter: "[ExternalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CourseOfferings_ExternalId",
                table: "CourseOfferings",
                column: "ExternalId",
                unique: true,
                filter: "[ExternalId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Students_ExternalId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Staffs_ExternalId",
                table: "Staffs");

            migrationBuilder.DropIndex(
                name: "IX_ScheduleSlots_ExternalId",
                table: "ScheduleSlots");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_ExternalId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Courses_ExternalId",
                table: "Courses");

            migrationBuilder.DropIndex(
                name: "IX_CourseOfferings_ExternalId",
                table: "CourseOfferings");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "ExternalUpdatedAt",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "ExternalVersion",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "LastSyncedAt",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "OriginSystem",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Staffs");

            migrationBuilder.DropColumn(
                name: "ExternalUpdatedAt",
                table: "Staffs");

            migrationBuilder.DropColumn(
                name: "ExternalVersion",
                table: "Staffs");

            migrationBuilder.DropColumn(
                name: "LastSyncedAt",
                table: "Staffs");

            migrationBuilder.DropColumn(
                name: "OriginSystem",
                table: "Staffs");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "ScheduleSlots");

            migrationBuilder.DropColumn(
                name: "ExternalUpdatedAt",
                table: "ScheduleSlots");

            migrationBuilder.DropColumn(
                name: "ExternalVersion",
                table: "ScheduleSlots");

            migrationBuilder.DropColumn(
                name: "LastSyncedAt",
                table: "ScheduleSlots");

            migrationBuilder.DropColumn(
                name: "OriginSystem",
                table: "ScheduleSlots");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ExternalUpdatedAt",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ExternalVersion",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "LastSyncedAt",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "OriginSystem",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "ExternalUpdatedAt",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "ExternalVersion",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "LastSyncedAt",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "OriginSystem",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "ExternalUpdatedAt",
                table: "CourseOfferings");

            migrationBuilder.DropColumn(
                name: "ExternalVersion",
                table: "CourseOfferings");

            migrationBuilder.DropColumn(
                name: "OriginSystem",
                table: "CourseOfferings");

            migrationBuilder.RenameColumn(
                name: "LastSyncedAt",
                table: "CourseOfferings",
                newName: "ExternalSyncedAt");

            migrationBuilder.RenameColumn(
                name: "ExternalId",
                table: "CourseOfferings",
                newName: "ExternalSystemId");
        }
    }
}
