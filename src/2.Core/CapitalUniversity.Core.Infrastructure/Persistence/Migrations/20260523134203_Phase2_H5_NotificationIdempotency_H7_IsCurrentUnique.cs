using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalUniversity.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase2_H5_NotificationIdempotency_H7_IsCurrentUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Semesters_AcademicYearId",
                table: "Semesters");

            migrationBuilder.DropIndex(
                name: "IX_Semesters_IsCurrent",
                table: "Semesters");

            migrationBuilder.DropIndex(
                name: "IX_AcademicYears_IsCurrent",
                table: "AcademicYears");

            migrationBuilder.AddColumn<Guid>(
                name: "IdempotencyKey",
                table: "Notifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Semesters_AcademicYearId_IsCurrent",
                table: "Semesters",
                columns: new[] { "AcademicYearId", "IsCurrent" },
                unique: true,
                filter: "[IsCurrent] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_IdempotencyKey",
                table: "Notifications",
                column: "IdempotencyKey",
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicYears_IsCurrent",
                table: "AcademicYears",
                column: "IsCurrent",
                unique: true,
                filter: "[IsCurrent] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Semesters_AcademicYearId_IsCurrent",
                table: "Semesters");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_IdempotencyKey",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_AcademicYears_IsCurrent",
                table: "AcademicYears");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Notifications");

            migrationBuilder.CreateIndex(
                name: "IX_Semesters_AcademicYearId",
                table: "Semesters",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_Semesters_IsCurrent",
                table: "Semesters",
                column: "IsCurrent",
                filter: "[IsCurrent] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicYears_IsCurrent",
                table: "AcademicYears",
                column: "IsCurrent",
                filter: "[IsCurrent] = 1");
        }
    }
}
