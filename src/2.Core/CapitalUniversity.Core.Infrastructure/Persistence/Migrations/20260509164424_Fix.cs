using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalUniversity.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Fix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StructureNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Order = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Path = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StructureNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StructureNodes_StructureNodes_ParentId",
                        column: x => x.ParentId,
                        principalTable: "StructureNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StructureNodes_IsDeleted",
                table: "StructureNodes",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_StructureNodes_Order",
                table: "StructureNodes",
                column: "Order");

            migrationBuilder.CreateIndex(
                name: "IX_StructureNodes_ParentId",
                table: "StructureNodes",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_StructureNodes_Path",
                table: "StructureNodes",
                column: "Path");

            migrationBuilder.CreateIndex(
                name: "IX_StructureNodes_Type",
                table: "StructureNodes",
                column: "Type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StructureNodes");
        }
    }
}
