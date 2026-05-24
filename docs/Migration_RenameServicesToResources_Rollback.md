# Migration 20260521174553 — `RenameServicesToResources_WithKey_DropDomain`

> Companion doc for the dedup migration. Migration assessment item **M10** in
> `BACKEND_FIX_PHASES.md` flagged that the migration runs raw-SQL grant dedup
> without an in-source rollback recipe. This file is the recipe.

## What the migration does

1. Renames the `Services` table to `Resources` (column `Key` added).
2. Drops the legacy `Domain` taxonomy column.
3. Re-keys foreign keys on `RolePermissions` and `StaffPermissionOverrides`
   so they point to `Resources.Id` instead of the dropped service path.
4. **Dedup pass (the risky part):** when two `Services` rows collapsed into
   one `Resources` row by the new natural key, the migration walks grants
   referencing the old service ids and rewrites them onto the surviving
   `Resources.Id`, merging duplicates to `MAX(Level)` to avoid downgrading a
   user that held the higher level on either source row.

The dedup logic is buried in raw `UPDATE` statements with `CASE` /
`COALESCE`. The bulk operations are correct as shipped; this doc exists so
that, if a future audit surfaces a row that should have been merged
differently, an operator can hand-fix the data without re-deriving the rules
from the migration source.

## Rollback recipe

A "rollback" here is a **data restore**, not a `migrations remove` —
information lost in the dedup pass cannot be recreated from the surviving
rows. Run the following on a fresh staging clone before touching production.

### Step 1 — restore a pre-migration backup

```sql
-- Take a full backup BEFORE running the migration in any environment.
-- The rollback procedure assumes a usable backup exists from immediately
-- before the migration ran.
RESTORE DATABASE [CapU_Rollback] FROM DISK = N'C:\Backups\CapU_pre_M10.bak'
WITH MOVE 'CapU' TO 'C:\Data\CapU_Rollback.mdf',
     MOVE 'CapU_Log' TO 'C:\Data\CapU_Rollback.ldf',
     REPLACE;
```

### Step 2 — diff the dedup outcome against the backup

```sql
SELECT
    r_now.RoleId,
    r_now.ResourceId,
    r_now.Level                     AS Level_NowProd,
    r_pre.Level                     AS Level_PreMigration,
    r_pre.ServiceId                 AS LegacyServiceId
FROM [CapU].dbo.RolePermissions r_now
JOIN [CapU_Rollback].dbo.RolePermissions r_pre
  ON r_pre.RoleId = r_now.RoleId
WHERE r_now.Level <> r_pre.Level;
```

Any row in this diff is a candidate to hand-restore. Apply the same query
to `StaffPermissionOverrides` to catch override changes.

### Step 3 — apply targeted UPDATEs to restore grants

```sql
BEGIN TRANSACTION;

UPDATE rp
SET    rp.Level = src.Level
FROM   [CapU].dbo.RolePermissions rp
JOIN   [CapU_Rollback].dbo.RolePermissions src
  ON   src.RoleId = rp.RoleId
   AND <map src.ServiceId → rp.ResourceId via the Resources.Key column>
WHERE  src.Level > rp.Level;

-- Confirm the row count matches the diff query above before COMMIT.
COMMIT TRANSACTION;
```

### Step 4 — invalidate the permission cache

After hand-restoring grants, push a permission cache epoch bump so live
sessions pick up the change:

```bash
# Bumps the global epoch via PermissionCacheInvalidator.InvalidateAllAsync().
curl -X POST https://<host>/api/permissions/cache/invalidate-all \
     -H "Authorization: Bearer <admin token>"
```

## What we will NOT roll back

- The `Services → Resources` rename itself. The application layer no longer
  references the legacy table; reverting the rename without a code rollback
  would leave the app pointed at a non-existent table.
- The dropped `Domain` column. The application no longer reads it; its
  contents are part of the backup if a forensic question arises.

## Avoiding this situation next time

- New migrations that touch grant tables should ship with a regression
  fixture in `Architecture.Tests` that snapshots pre- and post-migration row
  counts per role and asserts no downgrades. See the existing pattern in the
  permissions seeder self-healing tests.
- Raw-SQL dedup belongs in a stored procedure with INSERT INTO an
  `AuditTrail` table so a forensic audit doesn't have to reach back into a
  database backup at all.
