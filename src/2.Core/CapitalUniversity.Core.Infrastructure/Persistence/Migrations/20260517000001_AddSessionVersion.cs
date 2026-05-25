using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalUniversity.Core.Infrastructure.Persistence.Migrations
{
    // The [Migration] + [DbContext] attributes live here (normally generated in the
    // .Designer.cs partial). Without them EF's assembly scanner won't recognise
    // this file as a migration → MigrateAsync silently treats the DB as up-to-date.
    [DbContext(typeof(CoreDbContext))]
    [Migration("20260517000001_AddSessionVersion")]
    public partial class AddSessionVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SessionVersion",
                table: "Staffs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SessionVersion",
                table: "Students",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "SessionVersion", table: "Staffs");
            migrationBuilder.DropColumn(name: "SessionVersion", table: "Students");
        }
    }
}
