import { useState, useEffect, useRef, useMemo } from "react";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useQuery, useQueryClient, keepPreviousData } from "@tanstack/react-query";
import {
  Search, Filter, X, Download, Upload,
  CheckCircle, XCircle, Eye, Edit3,
  ChevronLeft, ChevronRight, Columns,
  RotateCcw, SlidersHorizontal,
} from "lucide-react";
import { useDomain } from "../../contexts/DomainContext";
import { useAcademic } from "../../contexts/AcademicContext";
import { useStickySelection } from "../../contexts/StickySelectionContext";
import { useToast } from "../Toast";
import { useColumnVisibility } from "../../hooks/useColumnVisibility";
import { getLocalized } from "../../utils/getLocalized";
import { SkeletonTable } from "../Skeleton";
import EmptyState from "../EmptyState";
import ConfirmDialog from "../ConfirmDialog";
import { usePermission } from "../../auth/usePermission";
import userService from "../../../modules/users/services/userService";
import "./directory.css";

const FILTER_OPTIONS_LOADERS = {
  faculties: () => userService.getFaculties(),
  programs: (facultyId) => userService.getPrograms(facultyId),
  levels: (programId) => userService.getLevels(programId),
  roles: () => userService.getRoles(),
};

const FILTER_FIELD_DEFS = {
  status: {
    type: "select", labelKey: "status", name: "isActive",
    options: [
      { value: "", labelKey: "all" },
      { value: "true", labelKey: "active" },
      { value: "false", labelKey: "inactive" },
    ],
  },
  passwordStatus: {
    type: "select", labelKey: "password_status", name: "passwordExpired",
    options: [
      { value: "", labelKey: "all" },
      { value: "false", labelKey: "valid" },
      { value: "true", labelKey: "expired" },
    ],
  },
  faculty: {
    type: "cascade", labelKey: "faculty", name: "facultyId",
    placeholderKey: "all_faculties", cascade: "program",
  },
  program: {
    type: "cascade", labelKey: "program", name: "programId",
    placeholderKey: "all_programs", dependsOn: "faculty", cascade: "level",
  },
  level: {
    type: "cascade", labelKey: "level", name: "levelId",
    placeholderKey: "all_levels", dependsOn: "program",
  },
  role: {
    type: "select", labelKey: "role", name: "role", options: [],
  },
  jobTitle: {
    type: "text", labelKey: "job_title", name: "jobTitle",
    placeholderKey: "job_title_placeholder",
  },
};

function DirectoryPage({ config }) {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const { addToast } = useToast();
  const { scopeNode } = useDomain();
  const { selectedYearObj, selectedSemesterObj, selectedYear, selectedSemester } = useAcademic();
  const { selected: pinnedUser } = useStickySelection();
  const { can } = usePermission();
  const queryClient = useQueryClient();
  const searchRef = useRef(null);

  // Defensive UI: render controls disabled with a reason instead of hiding them.
  const canInsert = can(config.permissionResource, 2);
  const canModify = can(config.permissionResource, 3);
  const canRemove = can(config.permissionResource, 5);
  const deniedTitle = (allowed, levelKey) =>
    (allowed ? undefined : t("requires_permission_level", {
      defaultValue: `Requires "${t(levelKey)}" access`,
      level: t(levelKey),
    }));
  const exportRef = useRef(null);
  const colVisRef = useRef(null);

  const { toggle, reset, orderedColumns, visibleKeys } = useColumnVisibility(config.id, config.columns);

  const [pagination, setPagination] = useState({ pageNumber: 1, pageSize: 20 });
  const [filters, setFilters] = useState({
    search: "", isActive: "", passwordExpired: "",
    facultyId: "", programId: "", levelId: "", role: "", jobTitle: "",
  });
  const [appliedFilters, setAppliedFilters] = useState({});
  const [showAdvanced, setShowAdvanced] = useState(false);
  const [selectedIds, setSelectedIds] = useState(new Set());
  const [showExportMenu, setShowExportMenu] = useState(false);
  const [exportMenuPos, setExportMenuPos] = useState({ top: 0, right: 20 });
  const [colVisOpen, setColVisOpen] = useState(false);
  const [colVisSearch, setColVisSearch] = useState("");
  const [colVisPos, setColVisPos] = useState({ top: 0, right: 0 });
  const colVisDropdownRef = useRef(null);
  const [bulkDeleteOpen, setBulkDeleteOpen] = useState(false);
  const [bulkActionPending, setBulkActionPending] = useState(false);

  const [cascadeOptions, setCascadeOptions] = useState({ faculties: [], programs: [], levels: [], roles: [] });

  useEffect(() => {
    FILTER_OPTIONS_LOADERS.faculties().then(f => setCascadeOptions(prev => ({ ...prev, faculties: f }))).catch(() => {});
    FILTER_OPTIONS_LOADERS.roles().then(r => setCascadeOptions(prev => ({ ...prev, roles: r }))).catch(() => {});
  }, []);

  useEffect(() => {
    if (!colVisOpen) return;
    const handle = (e) => {
      const btn = colVisRef.current;
      const dd = colVisDropdownRef.current;
      if (
        btn && !btn.contains(e.target) &&
        dd && !dd.contains(e.target)
      ) {
        setColVisOpen(false);
      }
    };
    document.addEventListener("mousedown", handle);
    return () => document.removeEventListener("mousedown", handle);
  }, [colVisOpen]);

  const activeFilterCount = useMemo(() => {
    return Object.values(appliedFilters).filter(v => v !== "").length;
  }, [appliedFilters]);

  // Scope, academic year and semester are part of the query key: switching
  // the global scope selector flips the key and refetches automatically.
  const requestParams = useMemo(() => ({
    Page: pagination.pageNumber,
    PageSize: pagination.pageSize,
    ScopeNodeId: scopeNode?.id || undefined,
    AcademicYearId: selectedYearObj?.id || undefined,
    SemesterId: selectedSemesterObj?.id || undefined,
    Search: appliedFilters.search || undefined,
    IsActive: appliedFilters.isActive !== undefined && appliedFilters.isActive !== ""
      ? appliedFilters.isActive === "true" : undefined,
    PasswordExpired: appliedFilters.passwordExpired !== undefined && appliedFilters.passwordExpired !== ""
      ? appliedFilters.passwordExpired === "true" : undefined,
    FacultyId: appliedFilters.facultyId || undefined,
    ProgramId: appliedFilters.programId || undefined,
    LevelId: appliedFilters.levelId || undefined,
    Role: appliedFilters.role || undefined,
    JobTitle: appliedFilters.jobTitle || undefined,
  }), [pagination.pageNumber, pagination.pageSize, appliedFilters, scopeNode?.id, selectedYearObj?.id, selectedSemesterObj?.id]);

  const listKey = useMemo(() => ["directory", config.id, requestParams], [config.id, requestParams]);

  const listQuery = useQuery({
    queryKey: listKey,
    queryFn: () => config.fetchFn(requestParams),
    placeholderData: keepPreviousData,
  });

  const data = listQuery.data?.items || [];
  const totalCount = listQuery.data?.totalCount || 0;
  const totalPages = listQuery.data?.totalPages || 1;
  const loading = listQuery.isPending;
  const error = listQuery.isError ? (listQuery.error?.message || "Failed to load data") : null;

  const detailRoute = (id) =>
    (config.routes.detail ? config.routes.detail(id) : `/admin/users/${id}`);

  const toggleableCols = config.columns.filter(c => !c.always);
  const visibleCount = visibleKeys.size;
  const totalToggleable = toggleableCols.length;
  const filteredColVisItems = useMemo(() => {
    if (!colVisSearch) return toggleableCols;
    const q = colVisSearch.toLowerCase();
    return toggleableCols.filter(col => t(col.labelKey).toLowerCase().includes(q));
  }, [toggleableCols, colVisSearch, t]);

  const handleFilterChange = (e) => {
    const { name, value } = e.target;
    setFilters(prev => ({ ...prev, [name]: value }));
    if (name === "facultyId") {
      setFilters(prev => ({ ...prev, programId: "", levelId: "" }));
      if (value) {
        FILTER_OPTIONS_LOADERS.programs(value).then(p => setCascadeOptions(prev => ({ ...prev, programs: p, levels: [] }))).catch(() => setCascadeOptions(prev => ({ ...prev, programs: [], levels: [] })));
      } else {
        setCascadeOptions(prev => ({ ...prev, programs: [], levels: [] }));
      }
    }
    if (name === "programId") {
      setFilters(prev => ({ ...prev, levelId: "" }));
      if (value) {
        FILTER_OPTIONS_LOADERS.levels(value).then(l => setCascadeOptions(prev => ({ ...prev, levels: l }))).catch(() => setCascadeOptions(prev => ({ ...prev, levels: [] })));
      } else {
        setCascadeOptions(prev => ({ ...prev, levels: [] }));
      }
    }
  };

  const applyFilters = () => {
    const cleaned = {};
    Object.entries(filters).forEach(([k, v]) => { if (v !== "") cleaned[k] = v; });
    setAppliedFilters(cleaned);
    setPagination(prev => ({ ...prev, pageNumber: 1 }));
    setSelectedIds(new Set());
  };

  const resetFilters = () => {
    setFilters({ search: "", isActive: "", passwordExpired: "", facultyId: "", programId: "", levelId: "", role: "", jobTitle: "" });
    setAppliedFilters({});
    setCascadeOptions(prev => ({ ...prev, programs: [], levels: [] }));
    setPagination(prev => ({ ...prev, pageNumber: 1 }));
    setSelectedIds(new Set());
    setShowAdvanced(false);
  };

  const clearSearch = () => {
    setFilters(prev => ({ ...prev, search: "" }));
    setAppliedFilters(prev => { const n = { ...prev }; delete n.search; return n; });
  };

  const removeFilterTag = (key) => {
    setFilters(prev => ({ ...prev, [key]: "" }));
    setAppliedFilters(prev => { const n = { ...prev }; delete n[key]; return n; });
  };

  const handleSearchKeyDown = (e) => { if (e.key === "Enter") applyFilters(); };

  const allSelected = data.length > 0 && data.every(d => selectedIds.has(d.id));
  const someSelected = data.some(d => selectedIds.has(d.id));

  const handleSelectAll = () => {
    if (allSelected) setSelectedIds(new Set());
    else setSelectedIds(new Set(data.map(d => d.id)));
  };

  const handleSelectOne = (id) => {
    setSelectedIds(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });
  };

  const performBulkAction = async (action) => {
    const ids = Array.from(selectedIds);
    if (!ids.length) return;

    const userType = config.userType === "student" ? "Student" : "Staff";
    const idSet = new Set(ids);
    let previous;

    // Activate/deactivate flip a flag we already render — apply optimistically
    // and roll back on failure. Delete stays pessimistic (rows must not vanish
    // until the server has really removed them).
    if (action === "activate" || action === "deactivate") {
      previous = queryClient.getQueryData(listKey);
      queryClient.setQueryData(listKey, (old) => old && {
        ...old,
        items: (old.items || []).map((item) =>
          idSet.has(item.id) ? { ...item, isActive: action === "activate" } : item),
      });
    }

    setBulkActionPending(true);
    try {
      let result;
      if (action === "activate") result = await userService.bulkActivateUsers(ids, userType);
      else if (action === "deactivate") result = await userService.bulkDeactivateUsers(ids, userType);
      else if (action === "delete") result = await userService.bulkDeleteUsers(ids, userType);
      if (result?.success) {
        addToast(t("bulk_action_done", {
          count: ids.length,
          defaultValue: `${ids.length} ${config.userType}(s) ${action === "activate" ? "activated" : action === "deactivate" ? "deactivated" : "deleted"}`,
        }), "success");
        setSelectedIds(new Set());
      } else if (previous !== undefined) {
        queryClient.setQueryData(listKey, previous);
      }
    } catch (err) {
      if (previous !== undefined) queryClient.setQueryData(listKey, previous);
      addToast(`Bulk ${action} failed: ${err.message}`, "error");
    } finally {
      setBulkActionPending(false);
      setBulkDeleteOpen(false);
      queryClient.invalidateQueries({ queryKey: ["directory", config.id] });
    }
  };

  const handleBulkAction = (action) => {
    if (!selectedIds.size) return;
    if (action === "delete") setBulkDeleteOpen(true);
    else performBulkAction(action);
  };

  const handleExport = async (format) => {
    setShowExportMenu(false);
    const baseParams = {
      ScopeNodeId: scopeNode?.id || undefined,
      AcademicYearId: selectedYearObj?.id || undefined,
      SemesterId: selectedSemesterObj?.id || undefined,
      Search: appliedFilters.search || undefined,
      IsActive: appliedFilters.isActive !== undefined && appliedFilters.isActive !== ""
        ? appliedFilters.isActive === "true" : undefined,
      PasswordExpired: appliedFilters.passwordExpired !== undefined && appliedFilters.passwordExpired !== ""
        ? appliedFilters.passwordExpired === "true" : undefined,
    };
    try {
      const exportFn = format === "csv" ? config.exportFns.csv : config.exportFns.excel;
      const blob = await exportFn(baseParams);
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `${config.userType}s_${new Date().toISOString().slice(0, 10)}.${format === "csv" ? "csv" : "xlsx"}`;
      document.body.appendChild(a);
      a.click();
      a.remove();
      window.URL.revokeObjectURL(url);
      addToast("Export completed", "success");
    } catch (err) {
      addToast(`Export failed: ${err.message}`, "error");
    }
  };

  const openExportMenu = () => {
    if (exportRef.current) {
      const rect = exportRef.current.getBoundingClientRect();
      setExportMenuPos({ top: rect.bottom + 6, right: window.innerWidth - rect.right });
    }
    setShowExportMenu(true);
  };

  const handleColVisToggle = () => {
    if (!colVisOpen && colVisRef.current) {
      const rect = colVisRef.current.getBoundingClientRect();
      setColVisPos({ top: rect.bottom + 6, right: window.innerWidth - rect.right });
    }
    setColVisOpen(o => !o);
    setColVisSearch("");
  };

  const toggleAllColumns = (show) => {
    toggleableCols.forEach(c => {
      const currentlyVisible = visibleKeys.has(c.key);
      if (show !== currentlyVisible) toggle(c.key);
    });
  };

  const firstItem = totalCount === 0 ? 0 : (pagination.pageNumber - 1) * pagination.pageSize + 1;
  const lastItem = Math.min(pagination.pageNumber * pagination.pageSize, totalCount);

  const getOptionLabel = (opt) => {
    return opt.localizedName || getLocalized(opt.name, i18n.language) || opt.name || "";
  };

  const renderFilterField = (fieldKey) => {
    const def = FILTER_FIELD_DEFS[fieldKey];
    if (!def) return null;

    if (def.type === "select") {
      const options = fieldKey === "role" ? cascadeOptions.roles : def.options;
      return (
        <div className="dir-filter-field" key={fieldKey}>
          <label><SlidersHorizontal size={11} />{t(def.labelKey)}</label>
          <select name={def.name} value={filters[def.name]} onChange={handleFilterChange}>
            {options.map((opt, i) => (
              <option key={i} value={opt.value}>{opt.labelKey ? t(opt.labelKey) : opt.value}</option>
            ))}
          </select>
        </div>
      );
    }

    if (def.type === "cascade") {
      const options = fieldKey === "faculty" ? cascadeOptions.faculties
        : fieldKey === "program" ? cascadeOptions.programs
        : cascadeOptions.levels;
      const disabled = def.dependsOn ? !filters[def.dependsOn + "Id"] : false;
      return (
        <div className="dir-filter-field" key={fieldKey}>
          <label><SlidersHorizontal size={11} />{t(def.labelKey)}</label>
          <select name={def.name} value={filters[def.name]} onChange={handleFilterChange} disabled={disabled}>
            <option value="">{t(def.placeholderKey)}</option>
            {options.map(opt => (
              <option key={opt.id} value={opt.id}>{getOptionLabel(opt)}</option>
            ))}
          </select>
        </div>
      );
    }

    if (def.type === "text") {
      return (
        <div className="dir-filter-field" key={fieldKey}>
          <label><SlidersHorizontal size={11} />{t(def.labelKey)}</label>
          <input type="text" name={def.name} value={filters[def.name]} onChange={handleFilterChange} placeholder={def.placeholderKey ? t(def.placeholderKey) : ""} />
        </div>
      );
    }

    return null;
  };

  const renderCell = (col, item) => {
    switch (col.key) {
      case "studentCode":
        return <span className="dir-cell-code">{item.studentCode}</span>;
      case "employeeCode":
        return <span className="dir-cell-code">{item.employeeCode}</span>;
      case "nationalId":
        return <span className="dir-cell-national-id">{item.nationalId}</span>;
      case "name":
        return <span className="dir-cell-name">{getLocalized(item.name, i18n.language)}</span>;
      case "email":
        return <span className="dir-cell-email">{item.email}</span>;
      case "phoneNumber":
        return <span>{item.phoneNumber || "—"}</span>;
      case "birthDate":
        return <span>{item.birthDate ? new Date(item.birthDate).toLocaleDateString() : "—"}</span>;
      case "facultyName":
        return <span>{item.facultyName || "—"}</span>;
      case "programName":
        return <span>{item.programName || "—"}</span>;
      case "levelName":
        return <span>{item.levelName || "—"}</span>;
      case "status":
        return <span className="dir-badge dir-badge-academic">{item.status || "—"}</span>;
      case "role":
        return <span className="dir-badge dir-badge-role">{item.role || "—"}</span>;
      case "jobTitle":
        return <span>{item.jobTitle || "—"}</span>;
      case "createdAt":
        return <span>{item.createdAt ? new Date(item.createdAt).toLocaleDateString() : "—"}</span>;
      case "isActive":
        return (
          <span className={`dir-badge ${item.isActive ? "dir-badge-active" : "dir-badge-inactive"}`}>
            <span className="dir-badge-dot" />
            {item.isActive ? t("active") : t("inactive")}
          </span>
        );
      case "passwordStatus":
        return (
          <span className={`dir-badge ${item.passwordStatus === "Expired" ? "dir-badge-expired" : "dir-badge-valid"}`}>
            {item.passwordStatus || t("valid")}
          </span>
        );
        case "actions":
          return (
            <div className="dir-action-btns" onClick={e => e.stopPropagation()}>
              <button className="dir-action-btn dir-action-btn-info" onClick={() => navigate(detailRoute(item.id))} title={t("details")}>
                <Eye size={15} />
              </button>
              <button
                className="dir-action-btn dir-action-btn-edit"
                onClick={() => canModify && navigate(config.routes.edit(item.id))}
                disabled={!canModify}
                title={deniedTitle(canModify, "edit") || t("edit")}
              >
                <Edit3 size={15} />
              </button>
            </div>
          );
      default:
        return null;
    }
  };

  return (
    <div className="dir-page">
      {/* ── Header ─────────────────────────── */}
      <div className="dir-header">
        <div className="dir-header-left">
          <div className="dir-header-icon"><config.icon size={20} /></div>
          <div>
            <span className="dir-header-kicker">{t(config.titleKey)}</span>
            <h1 className="dir-header-title">{t(config.titleKey)}</h1>
            <p className="dir-header-desc">{t(config.descriptionKey)}</p>
            <div className="dir-header-meta">
              <span className="dir-meta-chip">
                <strong>{t("scope_summary")}:</strong>{" "}
                {scopeNode?.localizedName || getLocalized(scopeNode?.name, i18n.language) || t("all_scopes")}
              </span>
              <span className="dir-meta-chip">
                <strong>{t("academic_year")}:</strong> {selectedYear}
              </span>
              <span className="dir-meta-chip">
                <strong>{t("semester")}:</strong> {selectedSemester}
              </span>
            </div>
          </div>
        </div>
        <div className="dir-header-actions">
          <button
            className="dir-btn dir-btn-ghost"
            onClick={openExportMenu}
            ref={exportRef}
            disabled={!canInsert}
            title={deniedTitle(canInsert, "insert")}
          >
            <Download size={14} /> {t("export")}
          </button>
          <button
            className="dir-btn dir-btn-primary"
            onClick={() => navigate(config.routes.add)}
            disabled={!canInsert}
            title={deniedTitle(canInsert, "insert")}
          >
            <Upload size={14} /> {t("add")}
          </button>
        </div>
      </div>

      {/* ── Column Visibility Dropdown ─────── */}
      {colVisOpen && (
        <div className="dir-colvis-dropdown" ref={colVisDropdownRef} style={{ position: "fixed", top: colVisPos.top, right: colVisPos.right, zIndex: 1000 }}>
          <div className="dir-colvis-header">
            <div className="dir-colvis-title">{t("show_hide_columns")}</div>
            <div className="dir-colvis-count">{visibleCount} of {totalToggleable} shown</div>
          </div>
          <div className="dir-colvis-search">
            <input
              type="text"
              placeholder={t("search_placeholder")}
              value={colVisSearch}
              onChange={e => setColVisSearch(e.target.value)}
              autoFocus
            />
          </div>
          <div className="dir-colvis-list">
            {filteredColVisItems.length === 0 ? (
              <div style={{ padding: "14px", textAlign: "center", fontSize: 12, color: "#9ca3af" }}>{t("no_data")}</div>
            ) : (
              filteredColVisItems.map(col => (
                <label key={col.key} className="dir-colvis-item" onClick={() => toggle(col.key)}>
                  <span className={`dir-colvis-toggle ${visibleKeys.has(col.key) ? "active" : ""}`} />
                  <span className="dir-colvis-item-label">{t(col.labelKey)}</span>
                </label>
              ))
            )}
          </div>
          <div className="dir-colvis-footer">
            <button type="button" className="dir-btn dir-btn-soft dir-btn-sm" onClick={() => toggleAllColumns(true)}>
              {t("show_all")}
            </button>
            <button type="button" className="dir-btn dir-btn-soft dir-btn-sm" onClick={() => toggleAllColumns(false)}>
              {t("hide_all")}
            </button>
            <button type="button" className="dir-btn dir-btn-soft dir-btn-sm" onClick={reset} style={{ marginLeft: "auto" }}>
              <RotateCcw size={11} /> {t("reset_to_default")}
            </button>
          </div>
        </div>
      )}

      {/* ── Export Menu ────────────────────── */}
      {showExportMenu && (
        <>
          <div className="dir-export-backdrop" onClick={() => setShowExportMenu(false)} />
          <div className="dir-export-menu" style={{ position: "fixed", top: exportMenuPos.top, right: exportMenuPos.right, zIndex: 1000 }}>
            <button onClick={() => handleExport("excel")}>Excel (.xlsx)</button>
            <button onClick={() => handleExport("csv")}>CSV (.csv)</button>
          </div>
        </>
      )}

      {/* ── Toolbar ────────────────────────── */}
      <div className="dir-toolbar">
        <div className="dir-toolbar-row">
          <div className="dir-search-box">
            <Search size={16} className="dir-search-icon" />
            <input
              ref={searchRef}
              type="text"
              name="search"
              value={filters.search}
              onChange={handleFilterChange}
              onKeyDown={handleSearchKeyDown}
              placeholder={t("search_placeholder")}
            />
            {filters.search && (
              <button type="button" className="dir-search-clear" onClick={clearSearch}><X size={14} /></button>
            )}
          </div>
          <button type="button" className="dir-btn dir-btn-navy" onClick={applyFilters}>
            <Search size={14} /> {t("search")}
          </button>
          <button
            type="button"
            className={`dir-btn dir-btn-soft ${showAdvanced ? "active" : ""}`}
            onClick={() => setShowAdvanced(!showAdvanced)}
          >
            <Filter size={14} />
            {t("filter")}
            {activeFilterCount > 0 && <span className="dir-filter-badge">{activeFilterCount}</span>}
          </button>
          <div ref={colVisRef}>
            <button type="button" className="dir-btn dir-btn-soft" onClick={handleColVisToggle}>
              <Columns size={14} /> {t("columns")}
              <span className="dir-colvis-badge">{visibleCount}/{totalToggleable}</span>
            </button>
          </div>
        </div>

        {showAdvanced && (
          <div className="dir-advanced-panel">
            <div className="dir-filter-grid">
              {config.filterFields.map(renderFilterField)}
            </div>
            <div className="dir-filter-actions">
              <button type="button" className="dir-btn dir-btn-soft dir-btn-sm" onClick={resetFilters}>
                <RotateCcw size={12} /> {t("reset")}
              </button>
              <button type="button" className="dir-btn dir-btn-primary dir-btn-sm" onClick={applyFilters}>
                {t("apply_filters")}
              </button>
            </div>
          </div>
        )}

        {activeFilterCount > 0 && (
          <div className="dir-active-tags">
            {Object.entries(appliedFilters).filter(([, v]) => v !== "").map(([key, value]) => (
              <span key={key} className="dir-tag">
                {t(FILTER_FIELD_DEFS[key]?.labelKey || key)}: {value}
                <button type="button" className="dir-tag-remove" onClick={() => removeFilterTag(key)}>
                  <X size={11} />
                </button>
              </span>
            ))}
            <button type="button" className="dir-btn dir-btn-soft dir-btn-xs" onClick={resetFilters}>
              <RotateCcw size={10} /> {t("clear_all")}
            </button>
          </div>
        )}
      </div>

      {/* ── Table ──────────────────────────── */}
      <div className="dir-table-section">
        <div className="dir-table-wrap">
          {loading ? (
            <div className="dir-loading-state">
              <SkeletonTable rows={8} columns={orderedColumns.length + 1} />
            </div>
          ) : error ? (
            <div className="dir-error-state">
              <p>{t("error")}</p>
              <p>{error}</p>
              <button className="dir-btn dir-btn-primary dir-btn-sm" onClick={() => listQuery.refetch()}>
                <RotateCcw size={12} /> {t("retry")}
              </button>
            </div>
          ) : data.length === 0 ? (
            <div className="dir-empty-state">
              <EmptyState
                icon={config.icon}
                title={t("no_data")}
                message={activeFilterCount > 0 ? "Try adjusting your search or filters" : ""}
                actionLabel={activeFilterCount > 0 ? t("reset") : ""}
                onAction={activeFilterCount > 0 ? resetFilters : undefined}
              />
            </div>
          ) : (
            <table className="dir-table">
              <thead>
                <tr>
                  <th className="dir-bulk-cell">
                    <input
                      type="checkbox"
                      className="dir-bulk-checkbox"
                      ref={el => { if (el) el.indeterminate = someSelected && !allSelected; }}
                      checked={allSelected}
                      onChange={handleSelectAll}
                    />
                  </th>
                  <th>#</th>
                  {orderedColumns.map(col => (
                    <th key={col.key}>{t(col.labelKey)}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {data.map((item, idx) => (
                  <tr
                    key={item.id}
                    className={`${selectedIds.has(item.id) ? "selected-row" : ""} ${pinnedUser?.id === item.id ? "pinned-row" : ""}`}
                    style={{ cursor: "pointer" }}
                    onClick={() => navigate(detailRoute(item.id))}
                  >
                    <td className="dir-bulk-cell" onClick={e => e.stopPropagation()}>
                      <input
                        type="checkbox"
                        className="dir-bulk-checkbox"
                        checked={selectedIds.has(item.id)}
                        onChange={() => handleSelectOne(item.id)}
                      />
                    </td>
                    <td>{(pagination.pageNumber - 1) * pagination.pageSize + idx + 1}</td>
                    {orderedColumns.map(col => (
                      <td key={col.key}>{renderCell(col, item)}</td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          )}

          {!loading && !error && data.length > 0 && totalPages > 1 && (
            <div className="dir-pagination">
              <button
                className="dir-page-btn"
                disabled={pagination.pageNumber <= 1}
                onClick={() => setPagination(prev => ({ ...prev, pageNumber: prev.pageNumber - 1 }))}
              >
                <ChevronLeft size={14} />
              </button>
              {Array.from({ length: Math.min(totalPages, 7) }, (_, i) => {
                let pageNum;
                if (totalPages <= 7) pageNum = i + 1;
                else {
                  const mid = Math.floor(7 / 2);
                  if (pagination.pageNumber <= mid + 1) pageNum = i + 1;
                  else if (pagination.pageNumber >= totalPages - mid) pageNum = totalPages - 7 + i + 1;
                  else pageNum = pagination.pageNumber - mid + i;
                }
                return (
                  <button
                    key={pageNum}
                    className={`dir-page-btn ${pageNum === pagination.pageNumber ? "active" : ""}`}
                    onClick={() => setPagination(prev => ({ ...prev, pageNumber: pageNum }))}
                  >
                    {pageNum}
                  </button>
                );
              })}
              <button
                className="dir-page-btn"
                disabled={pagination.pageNumber >= totalPages}
                onClick={() => setPagination(prev => ({ ...prev, pageNumber: prev.pageNumber + 1 }))}
              >
                <ChevronRight size={14} />
              </button>
            </div>
          )}
        </div>
      </div>

      {/* ── Footer ─────────────────────────── */}
      <div className="dir-footer">
        <div className="dir-page-size">
          <label>{t("show")}</label>
          <select
            value={pagination.pageSize}
            onChange={e => setPagination({ pageNumber: 1, pageSize: parseInt(e.target.value) })}
          >
            <option value="10">10</option>
            <option value="20">20</option>
            <option value="50">50</option>
            <option value="100">100</option>
          </select>
          <span>{t("entries_per_page")}</span>
        </div>
        <div className="dir-page-results">
          {t("showing_results", { first: firstItem, last: lastItem, total: totalCount })}
        </div>
      </div>

      {/* ── Bulk Action Bar ────────────────── */}
      {selectedIds.size > 0 && (
        <div className="dir-bulk-bar">
          <span className="dir-bulk-count">{selectedIds.size} {t("selected")}</span>
          <div className="dir-bulk-actions">
            {config.bulkActions?.activate !== false && (
              <button
                className="dir-bulk-btn dir-bulk-btn-activate"
                onClick={() => handleBulkAction("activate")}
                disabled={!canModify || bulkActionPending}
                title={deniedTitle(canModify, "edit")}
              >
                <CheckCircle size={13} /> {t("activate")}
              </button>
            )}
            {config.bulkActions?.deactivate !== false && (
              <button
                className="dir-bulk-btn dir-bulk-btn-deactivate"
                onClick={() => handleBulkAction("deactivate")}
                disabled={!canModify || bulkActionPending}
                title={deniedTitle(canModify, "edit")}
              >
                <XCircle size={13} /> {t("deactivate")}
              </button>
            )}
            <button
              className="dir-bulk-btn dir-bulk-btn-export"
              onClick={() => handleExport("excel")}
              disabled={!canInsert}
              title={deniedTitle(canInsert, "insert")}
            >
              <Download size={13} /> {t("export")}
            </button>
            {config.bulkActions?.delete !== false && (
              <button
                className="dir-bulk-btn dir-bulk-btn-delete"
                onClick={() => handleBulkAction("delete")}
                disabled={!canRemove || bulkActionPending}
                title={deniedTitle(canRemove, "delete")}
              >
                <XCircle size={13} /> {t("delete")}
              </button>
            )}
          </div>
        </div>
      )}

      <ConfirmDialog
        open={bulkDeleteOpen}
        onClose={() => { if (!bulkActionPending) setBulkDeleteOpen(false); }}
        onConfirm={() => performBulkAction("delete")}
        title={t("delete")}
        message={t("bulk_delete_confirm", {
          count: selectedIds.size,
          defaultValue: `Delete ${selectedIds.size} ${config.userType}(s)? This cannot be undone.`,
        })}
        confirmLabel={t("delete")}
        cancelLabel={t("cancel")}
        variant="danger"
        loading={bulkActionPending}
      />
    </div>
  );
}

export default DirectoryPage;
