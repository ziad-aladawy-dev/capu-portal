using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalUniversity.Core.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Renames the authorization <c>Services</c> table to <c>Resources</c> and
    /// introduces a typed <c>Key</c> column that holds the manifest-declared slug
    /// (e.g. <c>"invoices"</c>, <c>"academic-years"</c>). RolePermission and
    /// StaffPermissionOverride drop their denormalised <c>Resource</c> string
    /// columns — the FK to <c>Resources</c> is now the single source of truth.
    ///
    /// <para>
    /// StaffPermissionOverride also drops the <c>Domain</c> column, which was
    /// always written as <c>ScopeKeys.Global</c> and never read by any filter.
    /// </para>
    ///
    /// <para>
    /// The migration is in-place: the table is renamed (not dropped), existing
    /// rows get a backfilled <c>Key</c> using the same module/displayName mapping
    /// the deleted <c>PermissionIdentity.ResourceFor</c> used at runtime, then
    /// duplicate <c>(ModuleId, Key)</c> rows are collapsed — RolePermission FKs
    /// are redirected to a winning row per group, conflicting role grants are
    /// merged to <c>MAX(Level)</c>, and the losing rows are deleted. After
    /// dedup the unique index on <c>(ModuleId, Key)</c> can be applied safely.
    /// </para>
    ///
    /// <para>
    /// CourseOfferings + ScheduleSlots tables are also created here — they were
    /// pending in the model but had not yet had a migration scaffolded.
    /// </para>
    /// </summary>
    public partial class RenameServicesToResources_WithKey_DropDomain : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Drop FKs that reference Services so we can rename safely. ──
            migrationBuilder.DropForeignKey(
                name: "FK_RolePermissions_Services_ServiceId",
                table: "RolePermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_StaffPermissions_Services_ServiceId",
                table: "StaffPermissions");

            // ── 2. Add the Key column to Services with a temporary default so
            //      we can populate it before tightening the schema. ──
            migrationBuilder.AddColumn<string>(
                name: "Key",
                table: "Services",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            // ── 3. Backfill Key. This mirrors the old PermissionIdentity.ResourceFor
            //      mapping so existing rows round-trip against canonical PermissionNames:
            //        * "Manage Roles" (display)  → "roles"
            //        * Module "academics"        → "academic-years"
            //        * otherwise                 → Module.ModuleKey
            //      Manifest-declared keys (e.g. "invoices", "transactions") are
            //      already correct because the synchroniser would have written
            //      them on the next startup pass — but here we don't have those
            //      values, so falling back to ModuleKey is safe (the synchroniser
            //      treats existing rows as natural-key matches by (ModuleId, Key)
            //      and won't duplicate). ──
            migrationBuilder.Sql(@"
                UPDATE s
                SET    s.[Key] = CASE
                                    WHEN s.DisplayName = 'Manage Roles' THEN 'roles'
                                    WHEN m.ModuleKey   = 'academics'    THEN 'academic-years'
                                    ELSE m.ModuleKey
                                 END
                FROM   Services s
                JOIN   Modules  m ON m.Id = s.ModuleId;
            ");

            // ── 4. Deduplicate (ModuleId, Key) before the unique index is added.
            //      Within each (ModuleId, Key) group, keep the row with the
            //      smallest Id; redirect RolePermission and StaffPermission FKs
            //      from losers to the winner, then collapse the resulting
            //      RolePermission duplicates by MAX(Level) under the unique
            //      (RoleId, ServiceId) index. ──
            migrationBuilder.Sql(@"
                WITH winners AS (
                    SELECT s.Id              AS WinnerId,
                           s.ModuleId,
                           s.[Key],
                           ROW_NUMBER() OVER (PARTITION BY s.ModuleId, s.[Key] ORDER BY s.Id) AS rn
                    FROM   Services s
                ),
                redirects AS (
                    SELECT loser.Id  AS LoserId,
                           winner.WinnerId
                    FROM   winners loser
                    JOIN   winners winner
                        ON  winner.ModuleId = loser.ModuleId
                        AND winner.[Key]    = loser.[Key]
                        AND winner.rn       = 1
                    WHERE  loser.rn > 1
                )
                UPDATE rp
                SET    rp.ServiceId = r.WinnerId
                FROM   RolePermissions rp
                JOIN   redirects       r ON r.LoserId = rp.ServiceId;
            ");

            migrationBuilder.Sql(@"
                WITH winners AS (
                    SELECT s.Id              AS WinnerId,
                           s.ModuleId,
                           s.[Key],
                           ROW_NUMBER() OVER (PARTITION BY s.ModuleId, s.[Key] ORDER BY s.Id) AS rn
                    FROM   Services s
                ),
                redirects AS (
                    SELECT loser.Id  AS LoserId,
                           winner.WinnerId
                    FROM   winners loser
                    JOIN   winners winner
                        ON  winner.ModuleId = loser.ModuleId
                        AND winner.[Key]    = loser.[Key]
                        AND winner.rn       = 1
                    WHERE  loser.rn > 1
                )
                UPDATE sp
                SET    sp.ServiceId = r.WinnerId
                FROM   StaffPermissions sp
                JOIN   redirects        r ON r.LoserId = sp.ServiceId;
            ");

            // ── 5. Collapse RolePermission duplicates produced by the FK
            //      redirect: keep the row with MAX(Level) per (RoleId, ServiceId). ──
            migrationBuilder.Sql(@"
                WITH ranked AS (
                    SELECT Id,
                           ROW_NUMBER() OVER (
                               PARTITION BY RoleId, ServiceId
                               ORDER BY     Level DESC, Id
                           ) AS rn
                    FROM   RolePermissions
                )
                DELETE rp
                FROM   RolePermissions rp
                JOIN   ranked          r ON r.Id = rp.Id
                WHERE  r.rn > 1;
            ");

            // ── 6. Delete the now-orphaned losing Service rows. ──
            migrationBuilder.Sql(@"
                WITH ranked AS (
                    SELECT Id,
                           ROW_NUMBER() OVER (PARTITION BY ModuleId, [Key] ORDER BY Id) AS rn
                    FROM   Services
                )
                DELETE s
                FROM   Services s
                JOIN   ranked   r ON r.Id = s.Id
                WHERE  r.rn > 1;
            ");

            // ── 7. Rename the table + the existing OrderNumber sort index. ──
            migrationBuilder.RenameTable(
                name: "Services",
                newName: "Resources");

            migrationBuilder.RenameIndex(
                name: "IX_Services_ModuleId_OrderNumber",
                table: "Resources",
                newName: "IX_Resources_ModuleId_OrderNumber");

            // ── 8. Add the unique (ModuleId, Key) index — safe after dedup. ──
            migrationBuilder.CreateIndex(
                name: "IX_Resources_ModuleId_Key",
                table: "Resources",
                columns: new[] { "ModuleId", "Key" },
                unique: true);

            // ── 9. Rename FK columns + their indexes on the dependent tables. ──
            migrationBuilder.RenameColumn(
                name: "ServiceId",
                table: "StaffPermissions",
                newName: "ResourceId");

            migrationBuilder.RenameIndex(
                name: "IX_StaffPermissions_ServiceId",
                table: "StaffPermissions",
                newName: "IX_StaffPermissions_ResourceId");

            migrationBuilder.RenameColumn(
                name: "ServiceId",
                table: "RolePermissions",
                newName: "ResourceId");

            migrationBuilder.RenameIndex(
                name: "IX_RolePermissions_ServiceId",
                table: "RolePermissions",
                newName: "IX_RolePermissions_ResourceId");

            migrationBuilder.RenameIndex(
                name: "IX_RolePermissions_RoleId_ServiceId",
                table: "RolePermissions",
                newName: "IX_RolePermissions_RoleId_ResourceId");

            // ── 10. Drop the denormalised string Resource columns + dead Domain. ──
            migrationBuilder.DropColumn(name: "Resource", table: "RolePermissions");
            migrationBuilder.DropColumn(name: "Resource", table: "StaffPermissions");
            migrationBuilder.DropColumn(name: "Domain",   table: "StaffPermissions");

            // ── 11. Re-add the FKs pointing at the renamed Resources table. ──
            migrationBuilder.AddForeignKey(
                name: "FK_RolePermissions_Resources_ResourceId",
                table: "RolePermissions",
                column: "ResourceId",
                principalTable: "Resources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StaffPermissions_Resources_ResourceId",
                table: "StaffPermissions",
                column: "ResourceId",
                principalTable: "Resources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // ── 12. CourseOfferings + ScheduleSlots tables (model-tracked but
            //       not yet scaffolded into a migration). Kept here rather than
            //       split out because they're pending against the same snapshot. ──
            migrationBuilder.CreateTable(
                name: "CourseOfferings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SemesterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StructureNodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SectionCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    RegisteredCount = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RegistrationState = table.Column<int>(type: "int", nullable: false),
                    ExternalSystemId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ExternalSyncedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseOfferings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseOfferings_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourseOfferings_Semesters_SemesterId",
                        column: x => x.SemesterId,
                        principalTable: "Semesters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CourseOfferings_StructureNodes_StructureNodeId",
                        column: x => x.StructureNodeId,
                        principalTable: "StructureNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleSlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseOfferingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleSlots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourseOfferings_CourseId_SemesterId",
                table: "CourseOfferings",
                columns: new[] { "CourseId", "SemesterId" });

            migrationBuilder.CreateIndex(
                name: "IX_CourseOfferings_CourseId_SemesterId_StructureNodeId_SectionCode",
                table: "CourseOfferings",
                columns: new[] { "CourseId", "SemesterId", "StructureNodeId", "SectionCode" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CourseOfferings_SemesterId",
                table: "CourseOfferings",
                column: "SemesterId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseOfferings_StructureNodeId_SemesterId_Status",
                table: "CourseOfferings",
                columns: new[] { "StructureNodeId", "SemesterId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleSlots_CourseOfferingId_DayOfWeek_StartTime",
                table: "ScheduleSlots",
                columns: new[] { "CourseOfferingId", "DayOfWeek", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleSlots_CourseOfferingId_DayOfWeek_StartTime_EndTime",
                table: "ScheduleSlots",
                columns: new[] { "CourseOfferingId", "DayOfWeek", "StartTime", "EndTime" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ScheduleSlots");
            migrationBuilder.DropTable(name: "CourseOfferings");

            migrationBuilder.DropForeignKey(
                name: "FK_RolePermissions_Resources_ResourceId",
                table: "RolePermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_StaffPermissions_Resources_ResourceId",
                table: "StaffPermissions");

            migrationBuilder.AddColumn<string>(
                name: "Domain",
                table: "StaffPermissions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Resource",
                table: "StaffPermissions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Resource",
                table: "RolePermissions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.RenameColumn(
                name: "ResourceId",
                table: "StaffPermissions",
                newName: "ServiceId");

            migrationBuilder.RenameIndex(
                name: "IX_StaffPermissions_ResourceId",
                table: "StaffPermissions",
                newName: "IX_StaffPermissions_ServiceId");

            migrationBuilder.RenameColumn(
                name: "ResourceId",
                table: "RolePermissions",
                newName: "ServiceId");

            migrationBuilder.RenameIndex(
                name: "IX_RolePermissions_RoleId_ResourceId",
                table: "RolePermissions",
                newName: "IX_RolePermissions_RoleId_ServiceId");

            migrationBuilder.RenameIndex(
                name: "IX_RolePermissions_ResourceId",
                table: "RolePermissions",
                newName: "IX_RolePermissions_ServiceId");

            migrationBuilder.DropIndex(name: "IX_Resources_ModuleId_Key", table: "Resources");

            migrationBuilder.RenameIndex(
                name: "IX_Resources_ModuleId_OrderNumber",
                table: "Resources",
                newName: "IX_Services_ModuleId_OrderNumber");

            migrationBuilder.RenameTable(name: "Resources", newName: "Services");

            migrationBuilder.DropColumn(name: "Key", table: "Services");

            migrationBuilder.AddForeignKey(
                name: "FK_RolePermissions_Services_ServiceId",
                table: "RolePermissions",
                column: "ServiceId",
                principalTable: "Services",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StaffPermissions_Services_ServiceId",
                table: "StaffPermissions",
                column: "ServiceId",
                principalTable: "Services",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
