using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalUniversity.Core.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(CoreDbContext))]
    [Migration("20260517000003_AddPermissionFilterIndexes")]
    public partial class AddPermissionFilterIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hot-path indexes for PermissionService.GetPermissionsAsync. Without these
            // every permission lookup is a full table scan against StaffRoles +
            // StaffPermissions — fine at seed scale, catastrophic at 100k+ rows.

            migrationBuilder.CreateIndex(
                name: "IX_StaffRoles_StaffId_Year_Semester",
                table: "StaffRoles",
                columns: new[] { "StaffId", "Year", "Semester" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffRoles_StructureNodePath",
                table: "StaffRoles",
                column: "StructureNodePath");

            migrationBuilder.CreateIndex(
                name: "IX_StaffPermissions_StaffId_Year_Semester",
                table: "StaffPermissions",
                columns: new[] { "StaffId", "Year", "Semester" });

            migrationBuilder.CreateIndex(
                name: "IX_StaffPermissions_StructureNodePath",
                table: "StaffPermissions",
                column: "StructureNodePath");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_StaffRoles_StaffId_Year_Semester",        table: "StaffRoles");
            migrationBuilder.DropIndex(name: "IX_StaffRoles_StructureNodePath",            table: "StaffRoles");
            migrationBuilder.DropIndex(name: "IX_StaffPermissions_StaffId_Year_Semester",  table: "StaffPermissions");
            migrationBuilder.DropIndex(name: "IX_StaffPermissions_StructureNodePath",      table: "StaffPermissions");
        }
    }
}
