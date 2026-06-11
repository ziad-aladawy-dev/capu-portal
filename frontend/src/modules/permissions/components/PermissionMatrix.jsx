import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Search, X, ShieldOff } from "lucide-react";
import VirtualList from "../../../core/components/VirtualList";
import {
  ACTION_LEVELS, LABEL_TO_ACTION, getLevelLabels,
} from "../../../core/constants/permissionLevels";
import "../styles/permissionMatrix.css";

const ROW_HEIGHT = 44;
const VIRTUALIZE_THRESHOLD = 40;

// Dense roles-vs-permissions grid: every module's resources as rows, the six
// permission levels as columns, with a sticky column header and windowed rows
// for large permission catalogs.
//
// Defensive UI: when `canEdit` is false the level buttons stay visible but
// disabled, with a tooltip explaining why — never silently hidden.
function PermissionMatrix({
  modules,
  getLevel,
  onLevelChange,
  canEdit = true,
  disabledReason,
  height = 460,
  renderResourceExtra,
}) {
  const { t } = useTranslation();
  const levelLabels = getLevelLabels(t);
  const [search, setSearch] = useState("");

  // Available backend actions per resource — levels without a matching action
  // can't be granted and render as unavailable.
  const resourceActions = useMemo(() => {
    const map = {};
    for (const mod of modules || []) {
      for (const res of (mod.resources || [])) {
        const key = `${mod.moduleId}::${res.resourceId}`;
        map[key] = new Set((res.permissions || []).map((p) => p.action));
      }
    }
    return map;
  }, [modules]);

  const rows = useMemo(() => {
    const q = search.trim().toLowerCase();
    const out = [];
    for (const mod of modules || []) {
      const resources = (mod.resources || []).filter(
        (res) => !q || res.resourceName?.toLowerCase().includes(q)
      );
      if (resources.length === 0) continue;
      out.push({ type: "module", key: `m-${mod.moduleId}`, moduleName: mod.moduleName, count: resources.length });
      for (const res of resources) {
        out.push({ type: "resource", key: `${mod.moduleId}::${res.resourceId}`, moduleId: mod.moduleId, res });
      }
    }
    return out;
  }, [modules, search]);

  const editTooltip = canEdit ? undefined : (disabledReason || t("insufficient_permission", {
    defaultValue: "You don't have permission to change this",
  }));

  const renderRow = (row) => {
    if (row.type === "module") {
      return (
        <div className="pm-module-row" key={row.key}>
          <span className="pm-module-name">{row.moduleName}</span>
          <span className="pm-module-count">{row.count}</span>
        </div>
      );
    }
    const key = row.key;
    const currentLevel = getLevel(row.moduleId, row.res);
    return (
      <div className="pm-resource-row" key={key}>
        <span className="pm-resource-name" title={row.res.resourceName}>
          {row.res.resourceName}
          {renderResourceExtra?.(row.moduleId, row.res)}
        </span>
        <div className="pm-level-cells">
          {ACTION_LEVELS.map((l) => {
            const isLevelZero = l.value === 0;
            const backendAction = LABEL_TO_ACTION[l.label];
            const isAvailable = isLevelZero || (backendAction && resourceActions[key]?.has(backendAction));
            const active = isLevelZero ? currentLevel === 0 : currentLevel >= l.value;
            const disabled = !canEdit || !isAvailable;
            return (
              <button
                key={l.value}
                type="button"
                className={`pm-level-btn ${active ? "filled" : ""} ${currentLevel === l.value ? "current" : ""} ${disabled ? "disabled" : ""}`}
                onClick={() => !disabled && onLevelChange(row.moduleId, row.res, currentLevel === l.value ? 0 : l.value)}
                disabled={disabled}
                title={!canEdit ? editTooltip : !isAvailable ? t("level_unavailable", { defaultValue: "Not available for this resource" }) : levelLabels[l.value]}
                aria-label={`${row.res.resourceName}: ${levelLabels[l.value]}`}
              >
                {levelLabels[l.value]}
              </button>
            );
          })}
        </div>
      </div>
    );
  };

  const empty = (
    <div className="pm-empty">
      <ShieldOff size={28} />
      <p>{search ? t("no_results") : t("no_resources_module")}</p>
    </div>
  );

  return (
    <div className="pm-matrix">
      <div className="pm-toolbar">
        <div className="pm-search">
          <Search size={13} />
          <input
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder={t("search_resources", { defaultValue: "Search resources…" })}
          />
          {search && (
            <button type="button" className="pm-search-clear" onClick={() => setSearch("")}>
              <X size={12} />
            </button>
          )}
        </div>
        {!canEdit && (
          <span className="pm-readonly-badge" title={editTooltip}>
            <ShieldOff size={11} /> {t("read_only", { defaultValue: "Read only" })}
          </span>
        )}
      </div>

      <div className="pm-header-row">
        <span className="pm-header-resource">{t("resource", { defaultValue: "Resource" })}</span>
        <div className="pm-level-cells">
          {ACTION_LEVELS.map((l) => (
            <span key={l.value} className="pm-header-level">{levelLabels[l.value]}</span>
          ))}
        </div>
      </div>

      {rows.length === 0 ? empty : rows.length > VIRTUALIZE_THRESHOLD ? (
        <VirtualList
          items={rows}
          rowHeight={ROW_HEIGHT}
          height={height}
          overscan={8}
          rowKey={(row) => row.key}
          renderRow={(row) => renderRow(row)}
        />
      ) : (
        <div className="pm-rows" style={{ maxHeight: height, overflowY: "auto" }}>
          {rows.map((row) => renderRow(row))}
        </div>
      )}
    </div>
  );
}

export default PermissionMatrix;
