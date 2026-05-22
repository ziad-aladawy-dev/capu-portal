using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalUniversity.Core.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// OrderNumber on Service is a UI sort hint, never a business identity.
    /// The previous unique index on (ModuleId, OrderNumber) collided every
    /// fresh-DB boot — DataSeeder seeds Services at OrderNumber 0/1/2 with
    /// one set of DisplayNames, then PermissionManifestSynchronizer adds
    /// manifest-declared Services keyed on (ModuleId, DisplayName) and
    /// chose its own OrderNumbers that overlap with the seeded ones.
    /// Dropped to a plain index — still useful for sort, no longer a
    /// uniqueness invariant nothing else asserts.
    /// </summary>
    public partial class DropServicesOrderNumberUnique : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Services_ModuleId_OrderNumber",
                table: "Services");

            migrationBuilder.CreateIndex(
                name: "IX_Services_ModuleId_OrderNumber",
                table: "Services",
                columns: new[] { "ModuleId", "OrderNumber" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Services_ModuleId_OrderNumber",
                table: "Services");

            migrationBuilder.CreateIndex(
                name: "IX_Services_ModuleId_OrderNumber",
                table: "Services",
                columns: new[] { "ModuleId", "OrderNumber" },
                unique: true);
        }
    }
}
