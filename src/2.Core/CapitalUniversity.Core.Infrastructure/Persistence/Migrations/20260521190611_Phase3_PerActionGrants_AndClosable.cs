using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CapitalUniversity.Core.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Replaces the legacy <c>ActionLevel</c> ladder storage with per-action
    /// grant rows on both <c>RolePermissions</c> and <c>StaffPermissions</c>, and
    /// introduces the closable lifecycle flags (<c>IsClosed</c>, <c>ClosedAt</c>)
    /// on <c>AcademicYears</c> and <c>Semesters</c>.
    ///
    /// <para>
    /// The ladder is folded out via the canonical CRUD implies graph at backfill
    /// time — a single row with <c>Level=EditClose</c> becomes three rows
    /// (<c>View</c>, <c>Insert</c>, <c>EditClose</c>); <c>Delete</c> becomes
    /// five. After backfill the now-empty original rows are removed and the
    /// legacy <c>Level</c> + dead <c>PermissionId</c> columns are dropped. A
    /// new unique index covers <c>(RoleId, ResourceId, Action)</c> so the
    /// runtime can rely on at most one row per per-action grant.
    /// </para>
    /// </summary>
    public partial class Phase3_PerActionGrants_AndClosable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Closable lifecycle flags. Default IsClosed=false, ClosedAt
            //      nullable. Pure additive; no impact on existing rows. ──
            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "Semesters",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsClosed",
                table: "Semesters",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "AcademicYears",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsClosed",
                table: "AcademicYears",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // ── 2. Drop the legacy unique index that constrained a single row
            //      per (Role, Resource). The replacement covers (Role, Resource,
            //      Action) and is created in step 6 after backfill. ──
            migrationBuilder.DropIndex(
                name: "IX_RolePermissions_RoleId_ResourceId",
                table: "RolePermissions");

            // ── 3. Add the new Action column as nullable so backfill SQL can
            //      target it before NOT NULL is enforced. ──
            migrationBuilder.AddColumn<string>(
                name: "Action",
                table: "RolePermissions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Action",
                table: "StaffPermissions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            // ── 4. Backfill: expand each legacy Level row into one row per
            //      implied action under the canonical CRUD ladder.
            //        Level=1 (View)      → {View}
            //        Level=2 (Insert)    → {View, Insert}
            //        Level=3 (EditClose) → {View, Insert, EditClose}
            //        Level=4 (Open)      → {View, Insert, EditClose, Open}
            //        Level=5 (Delete)    → {View, Insert, EditClose, Open, Delete}
            //      We rewrite the lowest-level expansion in place (so existing
            //      Ids remain), then INSERT the higher actions. Original rows
            //      where Level=None are deleted. ──
            migrationBuilder.Sql(@"
                -- Rewrite the original row to hold action 'View'. Every non-None
                -- level implies View, so this is always safe.
                UPDATE RolePermissions SET [Action] = 'View' WHERE [Level] >= 1;
                UPDATE StaffPermissions SET [Action] = 'View' WHERE [Level] >= 1;

                -- INSERT one row per extra implied action, sharing scope columns.
                -- BaseEntity columns (Id, CreatedAt, IsDeleted) are populated; Id is
                -- a fresh Guid; CreatedAt copies the original row's timestamp.
                INSERT INTO RolePermissions (Id, RoleId, ResourceId, [Action], CreatedAt, UpdatedAt, IsDeleted)
                SELECT NEWID(), src.RoleId, src.ResourceId, n.action_name, src.CreatedAt, src.UpdatedAt, src.IsDeleted
                FROM   RolePermissions src
                CROSS APPLY (VALUES ('Insert', 2), ('EditClose', 3), ('Open', 4), ('Delete', 5)) AS n(action_name, action_level)
                WHERE  src.[Action] = 'View'   -- only the rows we just rewrote
                  AND  src.[Level] >= n.action_level;

                INSERT INTO StaffPermissions (Id, StaffId, ResourceId, [Action], StructureNodeId, StructureNodePath, Year, Semester, Type, ExpiresAt, CreatedAt, UpdatedAt, IsDeleted)
                SELECT NEWID(), src.StaffId, src.ResourceId, n.action_name, src.StructureNodeId, src.StructureNodePath, src.Year, src.Semester, src.Type, src.ExpiresAt, src.CreatedAt, src.UpdatedAt, src.IsDeleted
                FROM   StaffPermissions src
                CROSS APPLY (VALUES ('Insert', 2), ('EditClose', 3), ('Open', 4), ('Delete', 5)) AS n(action_name, action_level)
                WHERE  src.[Action] = 'View'
                  AND  src.[Level] >= n.action_level;

                -- Drop rows that had Level=None (no actions implied).
                DELETE FROM RolePermissions WHERE [Action] IS NULL;
                DELETE FROM StaffPermissions WHERE [Action] IS NULL;
            ");

            // ── 5. Now-empty Level + dead PermissionId columns can go. ──
            migrationBuilder.DropColumn(
                name: "Level",
                table: "StaffPermissions");

            migrationBuilder.DropColumn(
                name: "PermissionId",
                table: "StaffPermissions");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "RolePermissions");

            migrationBuilder.DropColumn(
                name: "PermissionId",
                table: "RolePermissions");

            // ── 6. Tighten Action to NOT NULL and add the per-action unique
            //      index. ──
            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "RolePermissions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Action",
                table: "StaffPermissions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId_ResourceId_Action",
                table: "RolePermissions",
                columns: new[] { "RoleId", "ResourceId", "Action" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse path is best-effort: per-action rows are collapsed back to
            // a single MAX(level) row per (Role, Resource). PermissionId is
            // resurrected with NEWID() placeholders — it was unused anyway.
            migrationBuilder.DropIndex(
                name: "IX_RolePermissions_RoleId_ResourceId_Action",
                table: "RolePermissions");

            migrationBuilder.AddColumn<int>(
                name: "Level",
                table: "RolePermissions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "PermissionId",
                table: "RolePermissions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.AddColumn<int>(
                name: "Level",
                table: "StaffPermissions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "PermissionId",
                table: "StaffPermissions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.Sql(@"
                ;WITH ranked AS (
                    SELECT Id, RoleId, ResourceId,
                           CASE [Action]
                               WHEN 'View'      THEN 1
                               WHEN 'Insert'    THEN 2
                               WHEN 'EditClose' THEN 3
                               WHEN 'Open'      THEN 4
                               WHEN 'Delete'    THEN 5
                               ELSE 0
                           END AS lvl
                    FROM   RolePermissions
                )
                UPDATE rp
                SET    rp.[Level] = ranked.lvl,
                       rp.PermissionId = NEWID()
                FROM   RolePermissions rp
                JOIN   ranked ON ranked.Id = rp.Id;

                ;WITH ranked AS (
                    SELECT Id,
                           CASE [Action]
                               WHEN 'View'      THEN 1
                               WHEN 'Insert'    THEN 2
                               WHEN 'EditClose' THEN 3
                               WHEN 'Open'      THEN 4
                               WHEN 'Delete'    THEN 5
                               ELSE 0
                           END AS lvl
                    FROM   StaffPermissions
                )
                UPDATE sp
                SET    sp.[Level] = ranked.lvl,
                       sp.PermissionId = NEWID()
                FROM   StaffPermissions sp
                JOIN   ranked ON ranked.Id = sp.Id;

                -- Collapse to single MAX(Level) row per (Role, Resource).
                ;WITH keepers AS (
                    SELECT Id, ROW_NUMBER() OVER (PARTITION BY RoleId, ResourceId ORDER BY [Level] DESC, Id) AS rn
                    FROM   RolePermissions
                )
                DELETE rp
                FROM   RolePermissions rp
                JOIN   keepers k ON k.Id = rp.Id
                WHERE  k.rn > 1;
            ");

            migrationBuilder.DropColumn(
                name: "Action",
                table: "RolePermissions");

            migrationBuilder.DropColumn(
                name: "Action",
                table: "StaffPermissions");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "Semesters");

            migrationBuilder.DropColumn(
                name: "IsClosed",
                table: "Semesters");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "AcademicYears");

            migrationBuilder.DropColumn(
                name: "IsClosed",
                table: "AcademicYears");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId_ResourceId",
                table: "RolePermissions",
                columns: new[] { "RoleId", "ResourceId" },
                unique: true);
        }
    }
}
