import { useState, useCallback, useEffect, useRef, useMemo } from "react";
import { useNavigate, useLocation } from "react-router-dom";
import {
  Search, X, Users,
  CalendarRange, BookOpen, User, RotateCcw,
  GraduationCap, ChevronDown, ChevronUp, Eye, Building2,
  UserSearch, Wallet, FileText, Shield, ClipboardList, GitBranch,
  Lock, ChevronsDown,
} from "lucide-react";
import { useTranslation } from "react-i18next";
import { getLocalized } from "../../utils/getLocalized";
import { getNodeTypeConfig } from "../../constants/nodeTypeRegistry";
import { useDomain } from "../../contexts/DomainContext";
import { useAcademic } from "../../contexts/AcademicContext";
import { useStickySelection } from "../../contexts/StickySelectionContext";
import { useScopeAwareUI } from "../../hooks/useScopeAwareUI";
import * as staffService from "../../services/staffService";
import * as studentService from "../../services/studentService";
import * as structureService from "../../services/structureService";
import * as permissionService from "../../services/permissionService";
import "./secondarySidebar.css";

const FILTER_DEFS = {
  staff: [
    { key: "status", labelKey: "status", options: [
      { value: "active", labelKey: "active" },
      { value: "inactive", labelKey: "inactive" },
    ]},
    { key: "role", labelKey: "role" },
    { key: "facultyId", labelKey: "faculty" },
    { key: "systemId", labelKey: "system" },
    { key: "programId", labelKey: "program" },
    { key: "departmentId", labelKey: "department" },
    { key: "nationalId", labelKey: "national_id" },
    { key: "email", labelKey: "email" },
  ],
  student: [
    { key: "status", labelKey: "status", options: [
      { value: "active", labelKey: "active" },
      { value: "inactive", labelKey: "inactive" },
    ]},
    { key: "enrollment", labelKey: "enrollment", options: [
      { value: "active", labelKey: "currently_enrolled" },
      { value: "graduated", labelKey: "graduated" },
    ]},
    { key: "level", labelKey: "level" },
    { key: "facultyId", labelKey: "faculty" },
    { key: "systemId", labelKey: "system" },
    { key: "programId", labelKey: "program" },
    { key: "specializationId", labelKey: "specialization" },
    { key: "nationalId", labelKey: "national_id" },
    { key: "email", labelKey: "email" },
    { key: "gender", labelKey: "gender", options: [
      { value: "Male", labelKey: "male" },
      { value: "Female", labelKey: "female" },
    ]},
  ],
};

const TABS = [
  { key: "student", labelKey: "students", icon: GraduationCap },
  { key: "staff", labelKey: "staff", icon: User },
];

// ── Pinned-user quick launch ──────────────────────────────────
// Registry of modules reachable for a pinned user. Adding a module
// here is all that's needed for it to show up in the launcher.
const PINNED_MODULES = [
  {
    key: "profile", labelKey: "profile", icon: UserSearch,
    types: ["staff", "student"],
    route: (u) => (u.type === "student" ? `/admin/students/${u.id}` : `/admin/users/${u.id}`),
  },
  {
    key: "academics", labelKey: "academic_hub", icon: GraduationCap,
    types: ["student"],
    route: (u) => `/admin/students/${u.id}/academics`,
  },
  {
    key: "service_requests", labelKey: "service_requests", icon: ClipboardList,
    types: ["student"],
    route: (u) => `/admin/students/${u.id}/service-requests`,
  },
  {
    key: "finance", labelKey: "finance", icon: Wallet,
    types: ["student"],
    route: (u) => `/admin/finance/treasury?studentId=${u.id}`,
  },
  {
    key: "profile_records", labelKey: "profile_records", icon: FileText,
    types: ["student"],
    route: (u) => `/admin/students/${u.id}/profile-records`,
  },
  {
    key: "assigned_workflows", labelKey: "assigned_workflows", icon: GitBranch,
    types: ["staff"],
    route: (u) => `/admin/users/${u.id}/assigned-workflows`,
  },
  {
    key: "permissions", labelKey: "permissions", icon: Shield,
    types: ["staff"],
    route: () => "/admin/permissions",
  },
];

const RAIL_SIZE = 3;        // pills shown before collapsing into "+N"
const FILTER_THRESHOLD = 8; // grid gets a filter box past this many modules
const USAGE_STORAGE_KEY = "secPinnedModuleUsage";

const readModuleUsage = () => {
  try { return JSON.parse(localStorage.getItem(USAGE_STORAGE_KEY)) || {}; }
  catch { return {}; }
};

const extractLevelNum = (name) => {
  if (!name) return "";
  const m = String(name).match(/(\d+)/);
  return m ? m[1] : name;
};

function PinnedQuickLaunch({ user }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { pathname } = useLocation();
  const [expanded, setExpanded] = useState(false);
  const [filter, setFilter] = useState("");
  // Usage counts are read per pin (not per click) so the rail doesn't
  // reshuffle under the employee's cursor mid-session.
  const [usage, setUsage] = useState(readModuleUsage);

  // Render-time reset when the pinned user changes (avoids an effect cascade).
  const [lastUserId, setLastUserId] = useState(user.id);
  if (lastUserId !== user.id) {
    setLastUserId(user.id);
    setExpanded(false);
    setFilter("");
    setUsage(readModuleUsage());
  }

  const available = useMemo(
    () => PINNED_MODULES.filter(m => m.types.includes(user.type)),
    [user.type],
  );

  // The rail is personalized: most-used modules first (stable sort keeps
  // registry order for ties). The expanded grid keeps registry order so
  // the full catalog stays predictable.
  const railItems = useMemo(
    () => [...available]
      .sort((a, b) => (usage[b.key] || 0) - (usage[a.key] || 0))
      .slice(0, RAIL_SIZE),
    [available, usage],
  );
  const overflowCount = available.length - railItems.length;

  // Longest matching route prefix wins, so /students/:id/academics
  // highlights Academics rather than Profile.
  const activeKey = useMemo(() => {
    let best = null;
    let bestLen = -1;
    available.forEach(m => {
      const path = m.route(user).split("?")[0];
      if ((pathname === path || pathname.startsWith(path + "/")) && path.length > bestLen) {
        best = m.key;
        bestLen = path.length;
      }
    });
    return best;
  }, [available, user, pathname]);

  const launch = (mod) => {
    const counts = readModuleUsage();
    counts[mod.key] = (counts[mod.key] || 0) + 1;
    try { localStorage.setItem(USAGE_STORAGE_KEY, JSON.stringify(counts)); } catch { /* ignore */ }
    setExpanded(false);
    setFilter("");
    navigate(mod.route(user));
  };

  const gridItems = useMemo(() => {
    if (!filter) return available;
    const q = filter.toLowerCase();
    return available.filter(m => t(m.labelKey).toLowerCase().includes(q));
  }, [available, filter, t]);

  if (available.length === 0) return null;

  return (
    <div className="sec-launch">
      <div className="sec-launch-rail">
        {railItems.map(mod => {
          const Icon = mod.icon;
          return (
            <button
              key={mod.key}
              className={`sec-launch-pill ${activeKey === mod.key ? "is-active" : ""}`}
              onClick={() => launch(mod)}
              title={t(mod.labelKey)}
            >
              <Icon size={11} />
              <span>{t(mod.labelKey)}</span>
            </button>
          );
        })}
        {overflowCount > 0 && (
          <button
            className={`sec-launch-more ${expanded ? "is-open" : ""}`}
            onClick={() => { setExpanded(e => !e); setFilter(""); }}
            title={expanded ? t("show_less") : t("more_actions")}
            aria-expanded={expanded}
          >
            {expanded ? <ChevronUp size={11} /> : <>+{overflowCount}</>}
          </button>
        )}
      </div>

      {expanded && (
        <div className="sec-launch-panel">
          {available.length > FILTER_THRESHOLD && (
            <div className="sec-launch-filter">
              <Search size={11} />
              <input
                type="text"
                value={filter}
                onChange={e => setFilter(e.target.value)}
                placeholder={t("find_action")}
                autoFocus
              />
            </div>
          )}
          <div className="sec-launch-grid">
            {gridItems.map(mod => {
              const Icon = mod.icon;
              return (
                <button
                  key={mod.key}
                  className={`sec-launch-tile ${activeKey === mod.key ? "is-active" : ""}`}
                  onClick={() => launch(mod)}
                  title={t(mod.labelKey)}
                >
                  <span className="sec-launch-tile-icon"><Icon size={13} /></span>
                  <span className="sec-launch-tile-label">{t(mod.labelKey)}</span>
                </button>
              );
            })}
            {gridItems.length === 0 && (
              <div className="sec-launch-empty">{t("no_results")}</div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}

function SecondarySidebar({ sidebarOpen, sidebarWidth }) {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const { scopeNode } = useDomain();
  const { preferredUserTab } = useScopeAwareUI();
  const { selectedYear, selectedSemester, selectedYearObj, selectedSemesterObj } = useAcademic();
  const { selected, select, clear, isActive } = useStickySelection();

  const [activeTab, setActiveTab] = useState("student");
  const [searchQuery, setSearchQuery] = useState("");
  const [filters, setFilters] = useState({});
  const [results, setResults] = useState([]);
  const [resultsLoading, setResultsLoading] = useState(false);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [loadMoreLoading, setLoadMoreLoading] = useState(false);
  const [showFilterPicker, setShowFilterPicker] = useState(false);
  const [visibleFilterKeys, setVisibleFilterKeys] = useState(["status", "level", "facultyId"]);
  const debounceRef = useRef(null);
  const [filterOptions, setFilterOptions] = useState({});
  const prevScopeIdRef = useRef(null);

  useEffect(() => {
    if (scopeNode?.id && scopeNode.id !== prevScopeIdRef.current) {
      // Map "all" to "student" since "all" tab was removed
      const tab = preferredUserTab === "all" ? "student" : preferredUserTab;
      setActiveTab(tab);
      setSearchQuery("");
      setFilters({});
      prevScopeIdRef.current = scopeNode.id;
    }
    if (!scopeNode) {
      prevScopeIdRef.current = null;
    }
  }, [scopeNode, preferredUserTab]);

  const effectiveDirType = activeTab === "all" ? "all" : activeTab;
  const activeFilters = FILTER_DEFS[effectiveDirType] || [];

  const scopeDisplayName = useMemo(() =>
    scopeNode?.localizedName || getLocalized(scopeNode?.name, i18n.language) || t("all_scopes"),
  [scopeNode, i18n.language, t]);

  useEffect(() => {
    const load = async () => {
      const options = {};
      try {
        if (effectiveDirType === "staff") {
          const roles = await permissionService.fetchAllRoles({ pageSize: 100 });
          options.roles = (roles?.items || []).map(r => ({
            value: r.id,
            label: getLocalized(r.name, i18n.language),
          }));
        }
        const levels = await structureService.fetchLevels();
        const seenOrders = new Set();
        options.levels = (levels || [])
          .sort((a, b) => a.order - b.order)
          .filter(l => {
            if (seenOrders.has(l.order)) return false;
            seenOrders.add(l.order);
            return true;
          })
          .map(l => ({
            value: l.order,
            label: extractLevelNum(getLocalized(l.name, i18n.language)),
          }));
        const faculties = await structureService.fetchFaculties();
        options.faculties = (faculties || []).map(f => ({
          value: f.id,
          label: getLocalized(f.name, i18n.language),
        }));
        // Fetch systems, programs, specializations for extended filters
        const systems = await structureService.fetchSystems();
        options.systems = (systems || []).map(s => ({
          value: s.id,
          label: getLocalized(s.name, i18n.language),
        }));
        const programs = await structureService.fetchPrograms();
        options.programs = (programs || []).map(p => ({
          value: p.id,
          label: getLocalized(p.name, i18n.language),
        }));
        const specializations = await structureService.fetchSpecializations();
        options.specializations = (specializations || []).map(sp => ({
          value: sp.id,
          label: getLocalized(sp.name, i18n.language),
        }));
      } catch { /* ignore */ }
      setFilterOptions(options);
    };
    load();
  }, [effectiveDirType, i18n.language]);

  const buildSearchParams = useCallback((query, activeFilters_) => {
    const params = {
      search: query || undefined,
      page: page,
      pageSize: 20,
      ScopeNodeId: scopeNode?.id || undefined,
      AcademicYearId: selectedYearObj?.id || undefined,
      SemesterId: selectedSemesterObj?.id || undefined,
    };
    // Merge scope-locked filters (unwrap {value, label} → value) with user-set filters.
    // User-set values take precedence — scoped defaults only apply when user hasn't touched them.
    const mergedFilters = {};
    Object.entries(lockedFilters).forEach(([k, v]) => { if (v && typeof v === "object" && v.value) mergedFilters[k] = v.value; });
    Object.assign(mergedFilters, activeFilters_);
    if (Object.keys(mergedFilters).length) {
      if (mergedFilters.role) params.role = mergedFilters.role;
      if (mergedFilters.status === "active") params.isActive = true;
      else if (mergedFilters.status === "inactive") params.isActive = false;
      if (mergedFilters.level !== undefined && mergedFilters.level !== "") params.levelOrder = mergedFilters.level;
      if (mergedFilters.enrollment === "graduated") params.isActive = false;
      if (mergedFilters.facultyId) params.facultyId = mergedFilters.facultyId;
      if (mergedFilters.systemId) params.systemId = mergedFilters.systemId;
      if (mergedFilters.programId) params.programId = mergedFilters.programId;
      if (mergedFilters.specializationId) params.specializationId = mergedFilters.specializationId;
      if (mergedFilters.departmentId) params.departmentId = mergedFilters.departmentId;
      if (mergedFilters.nationalId) params.nationalId = mergedFilters.nationalId;
      if (mergedFilters.email) params.email = mergedFilters.email;
      if (mergedFilters.gender) params.gender = mergedFilters.gender;
    }
    return params;
  }, [scopeNode, selectedYearObj, selectedSemesterObj, page]);

  const doSearch = useCallback(async (query, activeFilters_, pageNum = 1, append = false) => {
    const loadingSetter = pageNum === 1 ? setResultsLoading : setLoadMoreLoading;
    loadingSetter(true);
    try {
      const searchParams = buildSearchParams(query, activeFilters_);
      searchParams.page = pageNum;
      let allItems = [];
      let total = 0;
      if (effectiveDirType === "staff") {
        const data = await staffService.searchStaff(searchParams);
        total = data?.totalCount || data?.total || 0;
        if (data?.items) {
          allItems = [...allItems, ...data.items.map(r => ({
            id: r.id,
            name: getLocalized(r.name, i18n.language),
            code: r.employeeCode || "",
            type: "staff",
            role: r.role || "",
          }))];
        }
      } else {
        const data = await studentService.searchStudents(searchParams);
        total = data?.totalCount || data?.total || 0;
        if (data?.items) {
          allItems = [...allItems, ...data.items.map(r => ({
            id: r.id,
            name: getLocalized(r.name, i18n.language),
            code: r.studentCode || "",
            type: "student",
            level: extractLevelNum(r.levelName),
          }))];
        }
      }
      if (append) {
        setResults(prev => [...prev, ...allItems]);
      } else {
        setResults(allItems);
      }
      setTotalCount(total);
    } catch { if (!append) setResults([]); setTotalCount(0); }
    finally { loadingSetter(false); }
  }, [effectiveDirType, buildSearchParams, i18n.language]);

  useEffect(() => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    setPage(1);
    debounceRef.current = setTimeout(() => doSearch(searchQuery, filters, 1, false), 300);
    return () => {
      if (debounceRef.current) clearTimeout(debounceRef.current);
    };
  }, [searchQuery, filters, scopeNode, selectedYearObj, selectedSemesterObj, doSearch]);

  const lockedFilters = useMemo(() => {
    if (!scopeNode) return {};
    const type = scopeNode.type;
    const locked = {};
    // Map scope node type to locked filter keys + their values
    // Assumes scopeNode carries name/localizedName for display
    const scopeLabel = getLocalized(scopeNode.localizedName || scopeNode.name, i18n.language);
    if (type >= 2) { // Faculty or deeper
      locked.facultyId = { value: scopeNode.id, label: scopeLabel };
    }
    if (type >= 3) { // System or deeper
      locked.systemId = { value: scopeNode.id, label: scopeLabel };
    }
    if (type >= 4) { // Program or deeper
      locked.programId = { value: scopeNode.id, label: scopeLabel };
    }
    if (type >= 5) { // Level or deeper
      locked.level = { value: scopeNode.order ?? scopeNode.id, label: scopeLabel };
    }
    if (type >= 7) { // Specialization
      locked.specializationId = { value: scopeNode.id, label: scopeLabel };
    }
    return locked;
  }, [scopeNode, i18n.language]);

  const handleClearFilters = useCallback(() => {
    setSearchQuery("");
    setFilters({});
  }, []);

  const handleSelectEntity = useCallback((entity) => select(entity), [select]);

  const loadMore = useCallback(() => {
    const nextPage = page + 1;
    setPage(nextPage);
    doSearch(searchQuery, filters, nextPage, true);
  }, [page, searchQuery, filters, doSearch]);

  const handleTabChange = useCallback((tab) => {
    setActiveTab(tab);
    setFilters({});
    setSearchQuery("");
    setVisibleFilterKeys(["status", "level", "facultyId"]);
  }, []);

  const resolveOptions = (filter) => {
    if (filter.options) return filter.options;
    if (filter.key === "role" && filterOptions.roles) return filterOptions.roles;
    if (filter.key === "level" && filterOptions.levels) return filterOptions.levels;
    if (filter.key === "facultyId" && filterOptions.faculties) return filterOptions.faculties;
    if (filter.key === "systemId" && filterOptions.systems) return filterOptions.systems;
    if (filter.key === "programId" && filterOptions.programs) return filterOptions.programs;
    if (filter.key === "specializationId" && filterOptions.specializations) return filterOptions.specializations;
    return [];
  };

  const hasActiveFilters = Object.values(filters).some(v => v !== "");

  return (
    <aside
      className="secondary-sidebar"
      style={{ insetInlineStart: sidebarOpen ? sidebarWidth : 0 }}
    >
      {/* ── Tab Bar ──────────────────────── */}
      <div className="sec-tabs">
        {TABS.map(tab => {
          const Icon = tab.icon;
          return (
            <button
              key={tab.key}
              className={`sec-tab ${activeTab === tab.key ? "is-active" : ""}`}
              onClick={() => handleTabChange(tab.key)}
            >
              <Icon size={14} />
              <span>{t(tab.labelKey)}</span>
            </button>
          );
        })}
      </div>

      {/* ── Scope Section ────────────────── */}
      <div className="sec-scope">
        <div className="sec-scope-main">
          {(() => {
            const ScopeIcon = scopeNode ? (getNodeTypeConfig(scopeNode.type)?.icon || Building2) : Building2;
            const scopeColor = getNodeTypeConfig(scopeNode?.type)?.color || "inherit";
            return <ScopeIcon size={13} style={{ color: scopeColor }} />;
          })()}
          <div className="sec-scope-content">
            <span className="sec-scope-label">{t("scope_summary")}</span>
            <span className="sec-scope-value">{scopeDisplayName}</span>
          </div>
        </div>
        <div className="sec-scope-meta">
          <div className="sec-scope-meta-item">
            <CalendarRange size={10} />
            <span>{selectedYear}</span>
          </div>
          <div className="sec-scope-meta-divider" />
          <div className="sec-scope-meta-item">
            <BookOpen size={10} />
            <span>{selectedSemester}</span>
          </div>
        </div>
      </div>

      {/* ── Search + Filters ──────────────── */}
      <div className="sec-filters">
        <div className="sec-search-box">
          <Search size={13} />
          <input
            type="text"
            placeholder={t("sidebar_search_placeholder")}
            value={searchQuery}
            onChange={e => setSearchQuery(e.target.value)}
          />
          {searchQuery && (
            <button className="sec-search-clear" onClick={() => setSearchQuery("")}>
              <X size={12} />
            </button>
          )}
        </div>

        {/* Filter field visibility toggle */}
        <div className="sec-filter-fields-toggle">
          <button
            className="sec-filter-add-btn"
            onClick={() => setShowFilterPicker(p => !p)}
          >
            <ChevronsDown size={11} />
            <span>{t("filter_fields")}</span>
          </button>
          {showFilterPicker && (
            <div className="sec-filter-fields-panel">
              {activeFilters.map(filter => {
                const isLocked = lockedFilters[filter.key];
                return (
                  <label key={filter.key} className="sec-filter-field-option">
                    <input
                      type="checkbox"
                      checked={visibleFilterKeys.includes(filter.key) || !!isLocked}
                      disabled={!!isLocked}
                      onChange={() => {
                        setVisibleFilterKeys(prev =>
                          prev.includes(filter.key)
                            ? prev.filter(k => k !== filter.key)
                            : [...prev, filter.key]
                        );
                      }}
                    />
                    <span>{t(filter.labelKey)}</span>
                    {isLocked && <Lock size={10} className="sec-filter-lock-icon" />}
                  </label>
                );
              })}
            </div>
          )}
        </div>

        {activeFilters.filter(f => visibleFilterKeys.includes(f.key) || lockedFilters[f.key]).map(filter => {
          const isLocked = lockedFilters[filter.key];
          const lockInfo = lockedFilters[filter.key];
          const options = resolveOptions(filter);
          if (!options || options.length === 0) return null;
          return (
            <div className={`sec-filter-group ${isLocked ? "is-locked" : ""}`} key={filter.key}>
              <label className="sec-filter-label">
                {isLocked && <Lock size={9} className="sec-filter-lock-icon" />}
                {t(filter.labelKey)}
              </label>
              <div className="sec-filter-select-wrap">
                <select
                  className="sec-filter-select"
                  value={isLocked ? lockInfo.value : (filters[filter.key] || "")}
                  disabled={!!isLocked}
                  onChange={e => setFilters(prev => ({ ...prev, [filter.key]: e.target.value }))}
                  title={isLocked ? `${t("locked_to_scope")}: ${lockInfo.label}` : undefined}
                >
                  {isLocked ? (
                    <option value={lockInfo.value}>{lockInfo.label}</option>
                  ) : (
                    <>
                      <option value="">{t("all")}</option>
                      {options.map((opt, i) => (
                        <option key={i} value={opt.value}>{opt.labelKey ? t(opt.labelKey) : opt.label}</option>
                      ))}
                    </>
                  )}
                </select>
                <ChevronDown size={11} className="sec-filter-chevron" />
              </div>
            </div>
          );
        })}

        {hasActiveFilters && (
          <button className="sec-clear-btn" onClick={handleClearFilters}>
            <RotateCcw size={11} />
            {t("clear_filters")}
          </button>
        )}
      </div>

      {/* ── Pinned User Card (above results) ── */}
      {isActive && (
        <div className="sec-pinned-wrap">
          <div
            className="sec-pinned-card is-clickable"
            role="button"
            tabIndex={0}
            title={t("open_profile")}
            onClick={() => navigate(selected.type === "student" ? `/admin/students/${selected.id}` : `/admin/users/${selected.id}`)}
            onKeyDown={(e) => {
              if (e.key === "Enter" || e.key === " ") {
                e.preventDefault();
                navigate(selected.type === "student" ? `/admin/students/${selected.id}` : `/admin/users/${selected.id}`);
              }
            }}
          >
            <div className="sec-pinned-avatar">
              {selected.name?.charAt(0).toUpperCase()}
            </div>
            <div className="sec-pinned-info">
              <span className="sec-pinned-name">{selected.name}</span>
              <span className="sec-pinned-code">{selected.code}</span>
            </div>
            <span className={`sec-pinned-badge type-${selected.type}`}>
              {selected.type === "staff" ? t("user_type_staff") : t("user_type_student")}
            </span>
            <button
              className="sec-pinned-clear"
              onClick={(e) => { e.stopPropagation(); clear(); }}
              title={t("clear_selection")}
            >
              <X size={11} />
            </button>
          </div>
          <PinnedQuickLaunch user={selected} />
        </div>
      )}

      {/* ── Results ───────────────────────── */}
      <div className="sec-results">
        <div className="sec-results-header">
          {resultsLoading ? (
            <span className="sec-results-status">{t("searching")}</span>
          ) : (
            <span className="sec-results-status">
              {totalCount > 0 ? `${results.length}/${totalCount}` : results.length} {effectiveDirType === "staff" ? t("staff") : t("students")}
            </span>
          )}
          {!resultsLoading && results.length > 0 && (
            <span className="sec-results-hint"><Eye size={10} />{t("select_user_hint")}</span>
          )}
        </div>
        <div className="sec-results-list">
          {results.length === 0 && !resultsLoading && (
            <div className="sec-results-empty">
              {searchQuery || hasActiveFilters
                ? t("no_results")
                : <><Search size={20} /><span>{t("search_hint")}</span></>}
              {scopeNode && (
                <span className="sec-results-scope-hint">
                  {t("scoped_to")} {scopeDisplayName} — {t("scope_may_hide_results")}
                </span>
              )}
            </div>
          )}
          {resultsLoading && (
            <div className="sec-results-loading">
              <div className="sec-pulse" />
              <div className="sec-pulse" />
              <div className="sec-pulse" />
            </div>
          )}
          {results.map(entity => (
            <button
              key={entity.id}
              className={`sec-result-item ${isActive && selected?.id === entity.id ? "is-selected" : ""}`}
              onClick={() => handleSelectEntity(entity)}
            >
              <div className={`sec-result-avatar type-${entity.type}`}>
                {entity.name?.charAt(0).toUpperCase()}
              </div>
              <div className="sec-result-info">
                <strong className="sec-result-name">{entity.name}</strong>
                <span className="sec-result-code">{entity.code}</span>
                {entity.role && <span className="sec-result-meta">{entity.role}</span>}
                {entity.level && <span className="sec-result-meta">{entity.level}</span>}
              </div>
            </button>
          ))}
          {!resultsLoading && !loadMoreLoading && results.length > 0 && results.length < totalCount && (
            <button className="sec-load-more" onClick={loadMore}>
              <ChevronsDown size={12} />
              {t("load_more")}
            </button>
          )}
          {loadMoreLoading && (
            <div className="sec-results-loading">
              <div className="sec-pulse" />
            </div>
          )}
        </div>
      </div>
    </aside>
  );
}

export default SecondarySidebar;
