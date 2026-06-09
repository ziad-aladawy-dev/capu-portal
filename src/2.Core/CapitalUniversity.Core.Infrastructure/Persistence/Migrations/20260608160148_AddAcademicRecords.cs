using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalUniversity.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAcademicRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AcademicSummarySnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ExternalUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExternalVersion = table.Column<int>(type: "int", nullable: true),
                    LastSyncedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OriginSystem = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Gpa = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Cgpa = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    EarnedCredits = table.Column<int>(type: "int", nullable: false),
                    RemainingCredits = table.Column<int>(type: "int", nullable: false),
                    PassedHours = table.Column<int>(type: "int", nullable: false),
                    FailedHours = table.Column<int>(type: "int", nullable: false),
                    AcademicStanding = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicSummarySnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcademicSummarySnapshots_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentAcademicResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ExternalUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExternalVersion = table.Column<int>(type: "int", nullable: true),
                    LastSyncedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OriginSystem = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    StudentRegisteredCourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Grade = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    NumericScore = table.Column<decimal>(type: "decimal(6,3)", precision: 6, scale: 3, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreditsEarned = table.Column<int>(type: "int", nullable: false),
                    IsLatestAttempt = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentAcademicResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentAcademicResults_StudentRegisteredCourses_StudentRegisteredCourseId",
                        column: x => x.StudentRegisteredCourseId,
                        principalTable: "StudentRegisteredCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcademicSummarySnapshots_ExternalId",
                table: "AcademicSummarySnapshots",
                column: "ExternalId",
                unique: true,
                filter: "[ExternalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicSummarySnapshots_StudentId",
                table: "AcademicSummarySnapshots",
                column: "StudentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentAcademicResults_ExternalId",
                table: "StudentAcademicResults",
                column: "ExternalId",
                unique: true,
                filter: "[ExternalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAcademicResults_IsLatestAttempt",
                table: "StudentAcademicResults",
                column: "IsLatestAttempt");

            migrationBuilder.CreateIndex(
                name: "IX_StudentAcademicResults_StudentRegisteredCourseId",
                table: "StudentAcademicResults",
                column: "StudentRegisteredCourseId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcademicSummarySnapshots");

            migrationBuilder.DropTable(
                name: "StudentAcademicResults");
        }
    }
}
