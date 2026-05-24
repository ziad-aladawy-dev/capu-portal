using System;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalUniversity.Core.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Initial schema for the Student Services module. Creates eight tables:
    ///   <list type="bullet">
    ///     <item>StudentServices — service catalog (soft-deletable, code-unique).</item>
    ///     <item>StudentServiceRequests — request lifecycle rows (soft-deletable, indexed for queue queries).</item>
    ///     <item>ServiceFieldDefinitions / ServiceFieldValues — dynamic form fields + submissions.</item>
    ///     <item>ServiceDocumentDefinitions / ServiceDocumentSubmissions — required uploads + uploaded metadata.</item>
    ///     <item>StudentServiceWorkflows / *States / *Transitions — configurable workflow definitions.</item>
    ///   </list>
    ///
    /// <para>
    /// Hand-authored to match the project's existing migration style (see
    /// <c>AddPayments.cs</c> / <c>AddStudentProfileRecords.cs</c>). Soft-delete
    /// global query filters live in each entity's
    /// <see cref="Microsoft.EntityFrameworkCore.IEntityTypeConfiguration{TEntity}"/>
    /// — this migration only owns the table shape + indexes.
    /// </para>
    ///
    /// <para>
    /// Foreign keys to <c>Students</c> use <see cref="ReferentialAction.Restrict"/>
    /// (consistent with <c>StudentProfileRecords</c>). All intra-module
    /// parent→child relationships cascade so the admin-side delete of a
    /// service or workflow tears its configuration rows down atomically.
    /// </para>
    /// </summary>
    // [DbContext] / [Migration] attributes live on the .Designer.cs partial —
    // EF Core's MigrationsAssembly scanner reads them from there. Duplicating
    // them here is a compile error.
    public partial class AddStudentServices : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ----- Workflow definitions -----------------------------------
            migrationBuilder.CreateTable(
                name: "StudentServiceWorkflows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentServiceWorkflows", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentServiceWorkflows_Code",
                table: "StudentServiceWorkflows",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateTable(
                name: "StudentServiceWorkflowStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsInitial = table.Column<bool>(type: "bit", nullable: false),
                    IsTerminal = table.Column<bool>(type: "bit", nullable: false),
                    IsWaitingPayment = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
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

            migrationBuilder.CreateIndex(
                name: "IX_StudentServiceWorkflowStates_WorkflowDefinitionId_Status",
                table: "StudentServiceWorkflowStates",
                columns: new[] { "WorkflowDefinitionId", "Status" },
                unique: true);

            migrationBuilder.CreateTable(
                name: "StudentServiceWorkflowTransitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<int>(type: "int", nullable: false),
                    ToStatus = table.Column<int>(type: "int", nullable: false),
                    TransitionType = table.Column<int>(type: "int", nullable: false),
                    RequiredAction = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
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

            migrationBuilder.CreateIndex(
                name: "IX_StudentServiceWorkflowTransitions_WorkflowDefinitionId_FromStatus_ToStatus",
                table: "StudentServiceWorkflowTransitions",
                columns: new[] { "WorkflowDefinitionId", "FromStatus", "ToStatus" },
                unique: true);

            // ----- Service catalog ----------------------------------------
            migrationBuilder.CreateTable(
                name: "StudentServices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RequiresPayment = table.Column<bool>(type: "bit", nullable: false),
                    FeeType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    FeeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    EstimatedProcessingDays = table.Column<int>(type: "int", nullable: true),
                    AllowedProcessingRoleIdsCsv = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentServices", x => x.Id);
                });

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

            migrationBuilder.CreateTable(
                name: "ServiceFieldDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    FieldType = table.Column<int>(type: "int", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    MinLength = table.Column<int>(type: "int", nullable: true),
                    MaxLength = table.Column<int>(type: "int", nullable: true),
                    MinValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    MaxValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    DropdownValues = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
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

            migrationBuilder.CreateIndex(
                name: "IX_ServiceFieldDefinitions_StudentServiceId_Name",
                table: "ServiceFieldDefinitions",
                columns: new[] { "StudentServiceId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceFieldDefinitions_StudentServiceId_DisplayOrder",
                table: "ServiceFieldDefinitions",
                columns: new[] { "StudentServiceId", "DisplayOrder" });

            migrationBuilder.CreateTable(
                name: "ServiceDocumentDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    AllowedExtensions = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    MaxFileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
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

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDocumentDefinitions_StudentServiceId_Name",
                table: "ServiceDocumentDefinitions",
                columns: new[] { "StudentServiceId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDocumentDefinitions_StudentServiceId_DisplayOrder",
                table: "ServiceDocumentDefinitions",
                columns: new[] { "StudentServiceId", "DisplayOrder" });

            // ----- Request lifecycle --------------------------------------
            migrationBuilder.CreateTable(
                name: "StudentServiceRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentStatus = table.Column<int>(type: "int", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AssignedStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    PaymentReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentServiceRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentServiceRequests_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentServiceRequests_StudentServices_StudentServiceId",
                        column: x => x.StudentServiceId,
                        principalTable: "StudentServices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentServiceRequests_StudentId_CurrentStatus",
                table: "StudentServiceRequests",
                columns: new[] { "StudentId", "CurrentStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentServiceRequests_StudentServiceId_CurrentStatus",
                table: "StudentServiceRequests",
                columns: new[] { "StudentServiceId", "CurrentStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentServiceRequests_CurrentStatus_SubmittedAt",
                table: "StudentServiceRequests",
                columns: new[] { "CurrentStatus", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentServiceRequests_AssignedStaffId",
                table: "StudentServiceRequests",
                column: "AssignedStaffId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentServiceRequests_PaymentReferenceId",
                table: "StudentServiceRequests",
                column: "PaymentReferenceId");

            migrationBuilder.CreateTable(
                name: "ServiceFieldValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentServiceRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FieldDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceFieldValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceFieldValues_StudentServiceRequests_StudentServiceRequestId",
                        column: x => x.StudentServiceRequestId,
                        principalTable: "StudentServiceRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServiceFieldValues_ServiceFieldDefinitions_FieldDefinitionId",
                        column: x => x.FieldDefinitionId,
                        principalTable: "ServiceFieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceFieldValues_StudentServiceRequestId_FieldDefinitionId",
                table: "ServiceFieldValues",
                columns: new[] { "StudentServiceRequestId", "FieldDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceFieldValues_FieldDefinitionId",
                table: "ServiceFieldValues",
                column: "FieldDefinitionId");

            migrationBuilder.CreateTable(
                name: "ServiceDocumentSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentServiceRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceDocumentSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceDocumentSubmissions_StudentServiceRequests_StudentServiceRequestId",
                        column: x => x.StudentServiceRequestId,
                        principalTable: "StudentServiceRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServiceDocumentSubmissions_ServiceDocumentDefinitions_DocumentDefinitionId",
                        column: x => x.DocumentDefinitionId,
                        principalTable: "ServiceDocumentDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDocumentSubmissions_StudentServiceRequestId_DocumentDefinitionId",
                table: "ServiceDocumentSubmissions",
                columns: new[] { "StudentServiceRequestId", "DocumentDefinitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceDocumentSubmissions_DocumentDefinitionId",
                table: "ServiceDocumentSubmissions",
                column: "DocumentDefinitionId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse dependency order: leaves first, then service/workflow
            // roots. ReferentialAction.Cascade handles the within-service
            // children, but the migration framework requires explicit table
            // drops for each.
            migrationBuilder.DropTable(name: "ServiceDocumentSubmissions");
            migrationBuilder.DropTable(name: "ServiceFieldValues");
            migrationBuilder.DropTable(name: "StudentServiceRequests");
            migrationBuilder.DropTable(name: "ServiceDocumentDefinitions");
            migrationBuilder.DropTable(name: "ServiceFieldDefinitions");
            migrationBuilder.DropTable(name: "StudentServices");
            migrationBuilder.DropTable(name: "StudentServiceWorkflowTransitions");
            migrationBuilder.DropTable(name: "StudentServiceWorkflowStates");
            migrationBuilder.DropTable(name: "StudentServiceWorkflows");
        }
    }
}
