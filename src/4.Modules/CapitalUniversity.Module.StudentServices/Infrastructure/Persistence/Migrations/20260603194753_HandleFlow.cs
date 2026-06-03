using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalUniversity.Module.StudentServices.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HandleFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StudentRequests_Services_ServiceId",
                schema: "StudentServices",
                table: "StudentRequests");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowSteps_WorkflowId_StepKey",
                schema: "StudentServices",
                table: "WorkflowSteps");

            migrationBuilder.DropIndex(
                name: "IX_StudentRequests_SubmittedAt",
                schema: "StudentServices",
                table: "StudentRequests");

            migrationBuilder.DropIndex(
                name: "IX_RequestHistoryEntries_PerformedAt",
                schema: "StudentServices",
                table: "RequestHistoryEntries");

            migrationBuilder.DropIndex(
                name: "IX_RequestHistoryEntries_PerformedByUserId",
                schema: "StudentServices",
                table: "RequestHistoryEntries");

            migrationBuilder.DropIndex(
                name: "IX_RequestAttachments_StudentRequestId_StepKey",
                schema: "StudentServices",
                table: "RequestAttachments");

            migrationBuilder.DropColumn(
                name: "StepKey",
                schema: "StudentServices",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "ValidationRules",
                schema: "StudentServices",
                table: "WorkflowSteps");

            migrationBuilder.RenameColumn(
                name: "InputType",
                schema: "StudentServices",
                table: "WorkflowSteps",
                newName: "StepType");

            migrationBuilder.AlterColumn<string>(
                name: "PaymentTransactionId",
                schema: "StudentServices",
                table: "StudentRequests",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_StudentRequests_Services_ServiceId",
                schema: "StudentServices",
                table: "StudentRequests",
                column: "ServiceId",
                principalSchema: "StudentServices",
                principalTable: "Services",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StudentRequests_Services_ServiceId",
                schema: "StudentServices",
                table: "StudentRequests");

            migrationBuilder.RenameColumn(
                name: "StepType",
                schema: "StudentServices",
                table: "WorkflowSteps",
                newName: "InputType");

            migrationBuilder.AddColumn<string>(
                name: "StepKey",
                schema: "StudentServices",
                table: "WorkflowSteps",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ValidationRules",
                schema: "StudentServices",
                table: "WorkflowSteps",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PaymentTransactionId",
                schema: "StudentServices",
                table: "StudentRequests",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSteps_WorkflowId_StepKey",
                schema: "StudentServices",
                table: "WorkflowSteps",
                columns: new[] { "WorkflowId", "StepKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentRequests_SubmittedAt",
                schema: "StudentServices",
                table: "StudentRequests",
                column: "SubmittedAt");

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
                name: "IX_RequestAttachments_StudentRequestId_StepKey",
                schema: "StudentServices",
                table: "RequestAttachments",
                columns: new[] { "StudentRequestId", "StepKey" });

            migrationBuilder.AddForeignKey(
                name: "FK_StudentRequests_Services_ServiceId",
                schema: "StudentServices",
                table: "StudentRequests",
                column: "ServiceId",
                principalSchema: "StudentServices",
                principalTable: "Services",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
