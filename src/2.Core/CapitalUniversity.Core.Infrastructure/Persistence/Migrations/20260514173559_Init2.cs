using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalUniversity.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Init2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Staff_StructureNodes_StructureNodeId",
                table: "Staff");

            migrationBuilder.DropForeignKey(
                name: "FK_StaffPermissions_Staff_StaffId",
                table: "StaffPermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_StaffRoles_Staff_StaffId",
                table: "StaffRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_StructureNodes_StructureNodes_ParentId",
                table: "StructureNodes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Staff",
                table: "Staff");

            migrationBuilder.RenameTable(
                name: "Staff",
                newName: "Staffs");

            migrationBuilder.RenameIndex(
                name: "IX_Staff_StructureNodeId",
                table: "Staffs",
                newName: "IX_Staffs_StructureNodeId");

            migrationBuilder.RenameIndex(
                name: "IX_Staff_NationalId",
                table: "Staffs",
                newName: "IX_Staffs_NationalId");

            migrationBuilder.RenameIndex(
                name: "IX_Staff_EmployeeCode",
                table: "Staffs",
                newName: "IX_Staffs_EmployeeCode");

            migrationBuilder.AlterColumn<string>(
                name: "Path",
                table: "StructureNodes",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<int>(
                name: "Order",
                table: "StructureNodes",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                table: "StructureNodes",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "StructureNodes",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Notifications",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "Notifications",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordExpiry",
                table: "Staffs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Staffs",
                table: "Staffs",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_StructureNodes_Depth",
                table: "StructureNodes",
                column: "Depth");

            migrationBuilder.CreateIndex(
                name: "IX_StructureNodes_IsDeleted",
                table: "StructureNodes",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_StructureNodes_Order",
                table: "StructureNodes",
                column: "Order");

            migrationBuilder.CreateIndex(
                name: "IX_StructureNodes_Path",
                table: "StructureNodes",
                column: "Path");

            migrationBuilder.CreateIndex(
                name: "IX_StructureNodes_Type",
                table: "StructureNodes",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientUserId",
                table: "Notifications",
                column: "RecipientUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_StaffPermissions_Staffs_StaffId",
                table: "StaffPermissions",
                column: "StaffId",
                principalTable: "Staffs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StaffRoles_Staffs_StaffId",
                table: "StaffRoles",
                column: "StaffId",
                principalTable: "Staffs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Staffs_StructureNodes_StructureNodeId",
                table: "Staffs",
                column: "StructureNodeId",
                principalTable: "StructureNodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StructureNodes_StructureNodes_ParentId",
                table: "StructureNodes",
                column: "ParentId",
                principalTable: "StructureNodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StaffPermissions_Staffs_StaffId",
                table: "StaffPermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_StaffRoles_Staffs_StaffId",
                table: "StaffRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_Staffs_StructureNodes_StructureNodeId",
                table: "Staffs");

            migrationBuilder.DropForeignKey(
                name: "FK_StructureNodes_StructureNodes_ParentId",
                table: "StructureNodes");

            migrationBuilder.DropIndex(
                name: "IX_StructureNodes_Depth",
                table: "StructureNodes");

            migrationBuilder.DropIndex(
                name: "IX_StructureNodes_IsDeleted",
                table: "StructureNodes");

            migrationBuilder.DropIndex(
                name: "IX_StructureNodes_Order",
                table: "StructureNodes");

            migrationBuilder.DropIndex(
                name: "IX_StructureNodes_Path",
                table: "StructureNodes");

            migrationBuilder.DropIndex(
                name: "IX_StructureNodes_Type",
                table: "StructureNodes");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_RecipientUserId",
                table: "Notifications");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Staffs",
                table: "Staffs");

            migrationBuilder.DropColumn(
                name: "PasswordExpiry",
                table: "Staffs");

            migrationBuilder.RenameTable(
                name: "Staffs",
                newName: "Staff");

            migrationBuilder.RenameIndex(
                name: "IX_Staffs_StructureNodeId",
                table: "Staff",
                newName: "IX_Staff_StructureNodeId");

            migrationBuilder.RenameIndex(
                name: "IX_Staffs_NationalId",
                table: "Staff",
                newName: "IX_Staff_NationalId");

            migrationBuilder.RenameIndex(
                name: "IX_Staffs_EmployeeCode",
                table: "Staff",
                newName: "IX_Staff_EmployeeCode");

            migrationBuilder.AlterColumn<string>(
                name: "Path",
                table: "StructureNodes",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000);

            migrationBuilder.AlterColumn<int>(
                name: "Order",
                table: "StructureNodes",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<bool>(
                name: "IsDeleted",
                table: "StructureNodes",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "StructureNodes",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Staff",
                table: "Staff",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Staff_StructureNodes_StructureNodeId",
                table: "Staff",
                column: "StructureNodeId",
                principalTable: "StructureNodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StaffPermissions_Staff_StaffId",
                table: "StaffPermissions",
                column: "StaffId",
                principalTable: "Staff",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StaffRoles_Staff_StaffId",
                table: "StaffRoles",
                column: "StaffId",
                principalTable: "Staff",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StructureNodes_StructureNodes_ParentId",
                table: "StructureNodes",
                column: "ParentId",
                principalTable: "StructureNodes",
                principalColumn: "Id");
        }
    }
}
