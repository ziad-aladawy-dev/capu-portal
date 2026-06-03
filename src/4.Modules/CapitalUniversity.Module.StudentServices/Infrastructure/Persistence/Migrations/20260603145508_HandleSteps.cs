using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalUniversity.Module.StudentServices.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HandleSteps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FormFieldsJson",
                schema: "StudentServices",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "ScopeIsGlobalStructural",
                schema: "StudentServices",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "ScopeIsGlobalTemporal",
                schema: "StudentServices",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "ScopeSemester",
                schema: "StudentServices",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "ScopeStructureNodePath",
                schema: "StudentServices",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "ScopeYear",
                schema: "StudentServices",
                table: "Services");

            migrationBuilder.RenameColumn(
                name: "ScopeIncludeDescendants",
                schema: "StudentServices",
                table: "Services",
                newName: "IncludeDescendants");

            migrationBuilder.RenameColumn(
                name: "ScopeStructureNodeId",
                schema: "StudentServices",
                table: "Services",
                newName: "AcademicYearId");

            migrationBuilder.AlterColumn<Guid>(
                name: "WorkflowId",
                schema: "StudentServices",
                table: "Services",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<int>(
                name: "Type",
                schema: "StudentServices",
                table: "Services",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "StructureNode",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Path = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Depth = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StructureNode", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StructureNode_StructureNode_ParentId",
                        column: x => x.ParentId,
                        principalTable: "StructureNode",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WorkflowStepFields",
                schema: "StudentServices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FieldType = table.Column<int>(type: "int", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    OptionsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowStepFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowStepFields_WorkflowSteps_WorkflowStepId",
                        column: x => x.WorkflowStepId,
                        principalSchema: "StudentServices",
                        principalTable: "WorkflowSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceStructureNodes",
                schema: "StudentServices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StructureNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceStructureNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceStructureNodes_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalSchema: "StudentServices",
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServiceStructureNodes_StructureNode_StructureNodeId",
                        column: x => x.StructureNodeId,
                        principalTable: "StructureNode",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceStructureNodes_ServiceId_StructureNodeId",
                schema: "StudentServices",
                table: "ServiceStructureNodes",
                columns: new[] { "ServiceId", "StructureNodeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceStructureNodes_StructureNodeId",
                schema: "StudentServices",
                table: "ServiceStructureNodes",
                column: "StructureNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_StructureNode_ParentId",
                table: "StructureNode",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStepFields_WorkflowStepId_Order",
                schema: "StudentServices",
                table: "WorkflowStepFields",
                columns: new[] { "WorkflowStepId", "Order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceStructureNodes",
                schema: "StudentServices");

            migrationBuilder.DropTable(
                name: "WorkflowStepFields",
                schema: "StudentServices");

            migrationBuilder.DropTable(
                name: "StructureNode");

            migrationBuilder.DropColumn(
                name: "Type",
                schema: "StudentServices",
                table: "Services");

            migrationBuilder.RenameColumn(
                name: "IncludeDescendants",
                schema: "StudentServices",
                table: "Services",
                newName: "ScopeIncludeDescendants");

            migrationBuilder.RenameColumn(
                name: "AcademicYearId",
                schema: "StudentServices",
                table: "Services",
                newName: "ScopeStructureNodeId");

            migrationBuilder.AlterColumn<Guid>(
                name: "WorkflowId",
                schema: "StudentServices",
                table: "Services",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FormFieldsJson",
                schema: "StudentServices",
                table: "Services",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "ScopeIsGlobalStructural",
                schema: "StudentServices",
                table: "Services",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ScopeIsGlobalTemporal",
                schema: "StudentServices",
                table: "Services",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ScopeSemester",
                schema: "StudentServices",
                table: "Services",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScopeStructureNodePath",
                schema: "StudentServices",
                table: "Services",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScopeYear",
                schema: "StudentServices",
                table: "Services",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }
    }
}
