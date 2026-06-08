using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalUniversity.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentRegisteredCourses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudentRegisteredCourses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ExternalUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExternalVersion = table.Column<int>(type: "int", nullable: true),
                    LastSyncedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OriginSystem = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SemesterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StructureNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    RegistrationStatus = table.Column<int>(type: "int", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentRegisteredCourses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentRegisteredCourses_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentRegisteredCourses_Semesters_SemesterId",
                        column: x => x.SemesterId,
                        principalTable: "Semesters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentRegisteredCourses_StructureNodes_StructureNodeId",
                        column: x => x.StructureNodeId,
                        principalTable: "StructureNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentRegisteredCourses_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentRegisteredCourses_CourseId",
                table: "StudentRegisteredCourses",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentRegisteredCourses_ExternalId",
                table: "StudentRegisteredCourses",
                column: "ExternalId",
                unique: true,
                filter: "[ExternalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StudentRegisteredCourses_SemesterId",
                table: "StudentRegisteredCourses",
                column: "SemesterId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentRegisteredCourses_StructureNodeId",
                table: "StudentRegisteredCourses",
                column: "StructureNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentRegisteredCourses_StudentId_CourseId_AttemptNumber",
                table: "StudentRegisteredCourses",
                columns: new[] { "StudentId", "CourseId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentRegisteredCourses_StudentId_RegistrationStatus",
                table: "StudentRegisteredCourses",
                columns: new[] { "StudentId", "RegistrationStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentRegisteredCourses_StudentId_SemesterId",
                table: "StudentRegisteredCourses",
                columns: new[] { "StudentId", "SemesterId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentRegisteredCourses");
        }
    }
}
