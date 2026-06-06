using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalUniversity.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAcademicPlanCourseCourseFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AcademicPlanCourses_CourseId",
                table: "AcademicPlanCourses",
                column: "CourseId");

            migrationBuilder.AddForeignKey(
                name: "FK_AcademicPlanCourses_Courses_CourseId",
                table: "AcademicPlanCourses",
                column: "CourseId",
                principalTable: "Courses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AcademicPlanCourses_Courses_CourseId",
                table: "AcademicPlanCourses");

            migrationBuilder.DropIndex(
                name: "IX_AcademicPlanCourses_CourseId",
                table: "AcademicPlanCourses");
        }
    }
}
