using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalUniversity.Module.StudentServices.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialStudentServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "StudentServices");

            migrationBuilder.CreateTable(
                name: "Workflows",
                schema: "StudentServices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workflows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Services",
                schema: "StudentServices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsPaid = table.Column<bool>(type: "bit", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ScopeIsGlobalStructural = table.Column<bool>(type: "bit", nullable: false),
                    ScopeStructureNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ScopeIncludeDescendants = table.Column<bool>(type: "bit", nullable: false),
                    ScopeIsGlobalTemporal = table.Column<bool>(type: "bit", nullable: false),
                    ScopeYear = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ScopeSemester = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    WorkflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Services", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Services_Workflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalSchema: "StudentServices",
                        principalTable: "Workflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowSteps",
                schema: "StudentServices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    StepKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    InputType = table.Column<int>(type: "int", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    ValidationRules = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowSteps_Workflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalSchema: "StudentServices",
                        principalTable: "Workflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentRequests",
                schema: "StudentServices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PaymentStatus = table.Column<int>(type: "int", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PaymentTransactionId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SubmittedData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CurrentStepOrder = table.Column<int>(type: "int", nullable: false),
                    AssignedToStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentRequests_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalSchema: "StudentServices",
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowStepActions",
                schema: "StudentServices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowStepId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TriggersSubmission = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowStepActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowStepActions_WorkflowSteps_WorkflowStepId",
                        column: x => x.WorkflowStepId,
                        principalSchema: "StudentServices",
                        principalTable: "WorkflowSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RequestAttachments",
                schema: "StudentServices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StepKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    MimeType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequestAttachments_StudentRequests_StudentRequestId",
                        column: x => x.StudentRequestId,
                        principalSchema: "StudentServices",
                        principalTable: "StudentRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RequestHistoryEntries",
                schema: "StudentServices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PerformedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PerformedByRole = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PerformedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestHistoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequestHistoryEntries_StudentRequests_StudentRequestId",
                        column: x => x.StudentRequestId,
                        principalSchema: "StudentServices",
                        principalTable: "StudentRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RequestAttachments_StudentRequestId",
                schema: "StudentServices",
                table: "RequestAttachments",
                column: "StudentRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestAttachments_StudentRequestId_StepKey",
                schema: "StudentServices",
                table: "RequestAttachments",
                columns: new[] { "StudentRequestId", "StepKey" });

            migrationBuilder.CreateIndex(
                name: "IX_RequestHistoryEntries_PerformedAt",
                schema: "StudentServices",
                table: "RequestHistoryEntries",
                column: "PerformedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RequestHistoryEntries_PerformedByUserId",
                schema: "StudentServices",
                table: "RequestHistoryEntries",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestHistoryEntries_StudentRequestId",
                schema: "StudentServices",
                table: "RequestHistoryEntries",
                column: "StudentRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Services_WorkflowId",
                schema: "StudentServices",
                table: "Services",
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentRequests_AssignedToStaffId",
                schema: "StudentServices",
                table: "StudentRequests",
                column: "AssignedToStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentRequests_ServiceId",
                schema: "StudentServices",
                table: "StudentRequests",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentRequests_Status",
                schema: "StudentServices",
                table: "StudentRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_StudentRequests_StudentId",
                schema: "StudentServices",
                table: "StudentRequests",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentRequests_SubmittedAt",
                schema: "StudentServices",
                table: "StudentRequests",
                column: "SubmittedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStepActions_WorkflowStepId_ActionKey",
                schema: "StudentServices",
                table: "WorkflowStepActions",
                columns: new[] { "WorkflowStepId", "ActionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSteps_WorkflowId_Order",
                schema: "StudentServices",
                table: "WorkflowSteps",
                columns: new[] { "WorkflowId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSteps_WorkflowId_StepKey",
                schema: "StudentServices",
                table: "WorkflowSteps",
                columns: new[] { "WorkflowId", "StepKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RequestAttachments",
                schema: "StudentServices");

            migrationBuilder.DropTable(
                name: "RequestHistoryEntries",
                schema: "StudentServices");

            migrationBuilder.DropTable(
                name: "WorkflowStepActions",
                schema: "StudentServices");

            migrationBuilder.DropTable(
                name: "StudentRequests",
                schema: "StudentServices");

            migrationBuilder.DropTable(
                name: "WorkflowSteps",
                schema: "StudentServices");

            migrationBuilder.DropTable(
                name: "Services",
                schema: "StudentServices");

            migrationBuilder.DropTable(
                name: "Workflows",
                schema: "StudentServices");
        }
    }
}
