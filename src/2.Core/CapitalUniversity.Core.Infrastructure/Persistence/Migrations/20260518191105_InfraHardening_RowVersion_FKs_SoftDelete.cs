using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalUniversity.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InfraHardening_RowVersion_FKs_SoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "StudentProfileRecords",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Invoices",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Courses",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                collation: "SQL_Latin1_General_CP1_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AcademicPlans",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_StudentProfileRecords_StudentId_Category_CustomCategoryKey",
                table: "StudentProfileRecords",
                columns: new[] { "StudentId", "Category", "CustomCategoryKey" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_AcademicPlans_StructureNodes_StructureNodeId",
                table: "AcademicPlans",
                column: "StructureNodeId",
                principalTable: "StructureNodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Students_StudentId",
                table: "Invoices",
                column: "StudentId",
                principalTable: "Students",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StudentProfileRecords_Students_StudentId",
                table: "StudentProfileRecords",
                column: "StudentId",
                principalTable: "Students",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AcademicPlans_StructureNodes_StructureNodeId",
                table: "AcademicPlans");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Students_StudentId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_StudentProfileRecords_Students_StudentId",
                table: "StudentProfileRecords");

            migrationBuilder.DropIndex(
                name: "IX_StudentProfileRecords_StudentId_Category_CustomCategoryKey",
                table: "StudentProfileRecords");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "StudentProfileRecords");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "AcademicPlans");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "Courses",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32,
                oldCollation: "SQL_Latin1_General_CP1_CI_AS");
        }
    }
}
