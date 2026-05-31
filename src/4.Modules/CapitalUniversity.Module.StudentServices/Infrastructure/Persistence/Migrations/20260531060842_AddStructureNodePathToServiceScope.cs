using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalUniversity.Module.StudentServices.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStructureNodePathToServiceScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ScopeStructureNodePath",
                schema: "StudentServices",
                table: "Services",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScopeStructureNodePath",
                schema: "StudentServices",
                table: "Services");
        }
    }
}
