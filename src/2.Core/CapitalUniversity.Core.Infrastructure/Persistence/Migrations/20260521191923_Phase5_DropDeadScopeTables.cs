using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalUniversity.Core.Infrastructure.Persistence.Migrations
{
/// <summary>
    /// Drops the <c>RolePermissionScopes</c> and <c>StaffPermissionScopes</c>
    /// join tables. They never carried data — actual scope axes (Year, Semester,
    /// StructureNodeId, StructureNodePath) sit inline on the grant rows
    /// themselves, and no code ever wrote into the scope tables. Closing out
    /// the Phase 5 cleanup item "remove scopes tables".
    /// </summary>
    public partial class Phase5_DropDeadScopeTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RolePermissionScopes");

            migrationBuilder.DropTable(
                name: "StaffPermissionScopes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RolePermissionScopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RolePermissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    StructureNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissionScopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RolePermissionScopes_RolePermissions_RolePermissionId",
                        column: x => x.RolePermissionId,
                        principalTable: "RolePermissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StaffPermissionScopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StaffPermissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    StructureNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffPermissionScopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffPermissionScopes_StaffPermissions_StaffPermissionId",
                        column: x => x.StaffPermissionId,
                        principalTable: "StaffPermissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissionScopes_RolePermissionId",
                table: "RolePermissionScopes",
                column: "RolePermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffPermissionScopes_StaffPermissionId",
                table: "StaffPermissionScopes",
                column: "StaffPermissionId");
        }
    }
}
