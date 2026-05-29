using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalUniversity.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class @new : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceDocumentSubmissions");

            migrationBuilder.DropTable(
                name: "ServiceFieldValues");

            migrationBuilder.DropTable(
                name: "StudentServiceWorkflowStates");

            migrationBuilder.DropTable(
                name: "StudentServiceWorkflowTransitions");

            migrationBuilder.DropTable(
                name: "ServiceDocumentDefinitions");

            migrationBuilder.DropTable(
                name: "ServiceFieldDefinitions");

            migrationBuilder.DropTable(
                name: "StudentServiceRequests");

            migrationBuilder.DropTable(
                name: "StudentServiceWorkflows");

            migrationBuilder.DropTable(
                name: "StudentServices");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudentServices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllowedProcessingRoleIdsCsv = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    EstimatedProcessingDays = table.Column<int>(type: "int", nullable: true),
                    FeeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    FeeType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    RequiresPayment = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentServices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StudentServiceWorkflows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentServiceWorkflows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServiceDocumentDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllowedExtensions = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    MaxFileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceDocumentDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceDocumentDefinitions_StudentServices_StudentServiceId",
                        column: x => x.StudentServiceId,
                        principalTable: "StudentServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceFieldDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    DropdownValues = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    FieldType = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    MaxLength = table.Column<int>(type: "int", nullable: true),
                    MaxValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    MinLength = table.Column<int>(type: "int", nullable: true),
                    MinValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceFieldDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceFieldDefinitions_StudentServices_StudentServiceId",
                        column: x => x.StudentServiceId,
                        principalTable: "StudentServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentServiceRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CurrentStatus = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    PaymentReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentServiceRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentServiceRequests_StudentServices_StudentServiceId",
                        column: x => x.StudentServiceId,
                        principalTable: "StudentServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentServiceRequests_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentServiceWorkflowStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    IsInitial = table.Column<bool>(type: "bit", nullable: false),
                    IsTerminal = table.Column<bool>(type: "bit", nullable: false),
                    IsWaitingPayment = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentServiceWorkflowStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentServiceWorkflowStates_StudentServiceWorkflows_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "StudentServiceWorkflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentServiceWorkflowTransitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    RequiredAction = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
                    TransitionType = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentServiceWorkflowTransitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentServiceWorkflowTransitions_StudentServiceWorkflows_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "StudentServiceWorkflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceDocumentSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentServiceRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceDocumentSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceDocumentSubmissions_ServiceDocumentDefinitions_DocumentDefinitionId",
                        column: x => x.DocumentDefinitionId,
                        principalTable: "ServiceDocumentDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceDocumentSubmissions_StudentServiceRequests_StudentServiceRequestId",
                        column: x => x.StudentServiceRequestId,
                        principalTable: "StudentServiceRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceFieldValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FieldDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentServiceRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Value = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceFieldValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceFieldValues_ServiceFieldDefinitions_FieldDefinitionId",
                        column: x => x.FieldDefinitionId,
                        principalTable: "ServiceFieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceFieldValues_StudentServiceRequests_StudentServiceRequestId",
                        column: x => x.StudentServiceRequestId,
                        principalTable: "StudentServiceRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDocumentDefinitions_StudentServiceId_DisplayOrder",
                table: "ServiceDocumentDefinitions",
                columns: new[] { "StudentServiceId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDocumentDefinitions_StudentServiceId_Name",
                table: "ServiceDocumentDefinitions",
                columns: new[] { "StudentServiceId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDocumentSubmissions_DocumentDefinitionId",
                table: "ServiceDocumentSubmissions",
                column: "DocumentDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDocumentSubmissions_StudentServiceRequestId_DocumentDefinitionId",
                table: "ServiceDocumentSubmissions",
                columns: new[] { "StudentServiceRequestId", "DocumentDefinitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceFieldDefinitions_StudentServiceId_DisplayOrder",
                table: "ServiceFieldDefinitions",
                columns: new[] { "StudentServiceId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceFieldDefinitions_StudentServiceId_Name",
                table: "ServiceFieldDefinitions",
                columns: new[] { "StudentServiceId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceFieldValues_FieldDefinitionId",
                table: "ServiceFieldValues",
                column: "FieldDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceFieldValues_StudentServiceRequestId_FieldDefinitionId",
                table: "ServiceFieldValues",
                columns: new[] { "StudentServiceRequestId", "FieldDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentServiceRequests_AssignedStaffId",
                table: "StudentServiceRequests",
                column: "AssignedStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentServiceRequests_CurrentStatus_SubmittedAt",
                table: "StudentServiceRequests",
                columns: new[] { "CurrentStatus", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentServiceRequests_PaymentReferenceId",
                table: "StudentServiceRequests",
                column: "PaymentReferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentServiceRequests_StudentId_CurrentStatus",
                table: "StudentServiceRequests",
                columns: new[] { "StudentId", "CurrentStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentServiceRequests_StudentServiceId_CurrentStatus",
                table: "StudentServiceRequests",
                columns: new[] { "StudentServiceId", "CurrentStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentServices_Code",
                table: "StudentServices",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_StudentServices_IsActive",
                table: "StudentServices",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_StudentServiceWorkflows_Code",
                table: "StudentServiceWorkflows",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_StudentServiceWorkflowStates_WorkflowDefinitionId_Status",
                table: "StudentServiceWorkflowStates",
                columns: new[] { "WorkflowDefinitionId", "Status" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentServiceWorkflowTransitions_WorkflowDefinitionId_FromStatus_ToStatus",
                table: "StudentServiceWorkflowTransitions",
                columns: new[] { "WorkflowDefinitionId", "FromStatus", "ToStatus" },
                unique: true);
        }
    }
}
