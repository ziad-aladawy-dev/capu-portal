using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalUniversity.Sync.Student.Migrations
{
    /// <inheritdoc />
    public partial class StudentInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "sync_student");

            migrationBuilder.CreateTable(
                name: "students",
                schema: "sync_student",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalStudentId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ExternalUpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExternalVersion = table.Column<int>(type: "int", nullable: false),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OriginSystem = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_students", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_students_ExternalStudentId",
                schema: "sync_student",
                table: "students",
                column: "ExternalStudentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_students_ExternalUpdatedAt",
                schema: "sync_student",
                table: "students",
                column: "ExternalUpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "students",
                schema: "sync_student");
        }
    }
}