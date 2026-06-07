import { useState, useCallback, useEffect, useRef, useMemo } from "react";
import {
  Search, X, Users,
  CalendarRange, BookOpen, User, RotateCcw,
  GraduationCap, ChevronDown, Eye, Building2,
} from "lucide-react";
import { useTranslation } from "react-i18next";
import { getLocalized } from "../../utils/getLocalized";
import { getNodeTypeConfig } from "../../../modules/university/utils/nodeTypeRegistry";
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
    { key: "role", labelKey: "role" },
    { key: "status", labelKey: "status", options: [
      { value: "active", labelKey: "active" },
      { value: "inactive", labelKey: "inactive" },
    ]},
    { key: "facultyId", labelKey: "faculty" },
  ],
  student: [
    { key: "level", labelKey: "level" },
    { key: "status", labelKey: "status", options: [
      { value: "active", labelKey: "active" },
      { value: "inactive", labelKey: "inactive" },
    ]},
    { key: "facultyId", labelKey: "faculty" },
    { key: "enrollment", labelKey: "enrollment", options: [
      { value: "active", labelKey: "currently_enrolled" },
      { value: "graduated", labelKey: "graduated" },
    ]},
  ],
  all: [
    { key: "role", labelKey: "role" },
    { key: "level", labelKey: "level" },
    { key: "status", labelKey: "status", options: [
      { value: "active", labelKey: "active" },
      { value: "inactive", labelKey: "inactive" },
    ]},
    { key: "facultyId", labelKey: "faculty" },
  ],
};

const TABS = [
  { key: "all", labelKey: "all_users", icon: Users },
  { key: "staff", labelKey: "staff", icon: User },
  { key: "student", labelKey: "students", icon: GraduationCap },
];

function SecondarySidebar({ sidebarOpen, sidebarWidth }) {
  const { t, i18n } = useTranslation();
  const { scopeNode } = useDomain();
  const { preferredUserTab } = useScopeAwareUI();
  const { selectedYear, selectedSemester, selectedYearObj, selectedSemesterObj } = useAcademic();
  const { selected, select, clear, isActive } = useStickySelection();

  const [activeTab, setActiveTab] = useState("all");
  const [searchQuery, setSearchQuery] = useState("");
  const [filters, setFilters] = useState({});
  const [results, setResults] = useState([]);
  const [resultsLoading, setResultsLoading] = useState(false);
  const debounceRef = useRef(null);
  const [filterOptions, setFilterOptions] = useState({});
  const prevScopeIdRef = useRef(null);

  useEffect(() => {
    if (scopeNode?.id && scopeNode.id !== prevScopeIdRef.current) {
      setActiveTab(preferredUserTab);
      setSearchQuery("");
      setFilters({});
      prevScopeIdRef.current = scopeNode.id;
    }
    if (!scopeNode) {
      prevScopeIdRef.current = null;
    }
  }, [scopeNode, preferredUserTab]);

  const effectiveDirType = activeTab === "all" ? "all" : activeTab;
  const activeFilters = FILTER_DEFS[effectiveDirType] || FILTER_DEFS.all;

  const scopeDisplayName = useMemo(() =>
    scopeNode?.localizedName || getLocalized(scopeNode?.name, i18n.language) || t("all_scopes"),
  [scopeNode, i18n.language, t]);

  useEffect(() => {
    const load = async () => {
      const options = {};
      try {
        if (effectiveDirType === "staff" || effectiveDirType === "all") {
          const roles = await permissionService.fetchAllRoles({ pageSize: 100 });
          options.roles = (roles?.items || []).map(r => ({
            value: r.id,
            label: getLocalized(r.name, i18n.language),
          }));
        }
        const levels = await structureService.fetchLevels();
        options.levels = (levels || []).map(l => ({
          value: l.id,
          label: getLocalized(l.name, i18n.language),
        }));
        const faculties = await structureService.fetchFaculties();
        options.faculties = (faculties || []).map(f => ({
          value: f.id,
          label: getLocalized(f.name, i18n.language),
        }));
      } catch { /* ignore */ }
      setFilterOptions(options);
    };
    load();
  }, [effectiveDirType, i18n.language]);

  const buildSearchParams = useCallback((query, activeFilters_) => {
    const params = {
      search: query || undefined,
      page: 1,
      pageSize: 20,
      ScopeNodeId: scopeNode?.id || undefined,
      AcademicYearId: selectedYearObj?.id || undefined,
      SemesterId: selectedSemesterObj?.id || undefined,
    };
    if (activeFilters_) {
      if (activeFilters_.role) params.role = activeFilters_.role;
      if (activeFilters_.status === "active") params.isActive = true;
      else if (activeFilters_.status === "inactive") params.isActive = false;
      if (activeFilters_.level) params.levelId = activeFilters_.level;
      if (activeFilters_.enrollment === "graduated") params.isActive = false;
      if (activeFilters_.facultyId) params.facultyId = activeFilters_.facultyId;
    }
    return params;
  }, [scopeNode, selectedYearObj, selectedSemesterObj]);

  const doSearch = useCallback(async (query, activeFilters_) => {
    setResultsLoading(true);
    try {
      const searchParams = buildSearchParams(query, activeFilters_);
      let allItems = [];
      if (effectiveDirType === "staff" || effectiveDirType === "all") {
        const data = await staffService.searchStaff(searchParams);
        if (data?.items) {
          allItems = [...allItems, ...data.items.map(r => ({
            id: r.id,
            name: getLocalized(r.name, i18n.language),
            code: r.employeeCode || "",
            type: "staff",
            role: r.role || "",
          }))];
        }
      }
      if (effectiveDirType === "student" || effectiveDirType === "all") {
        const data = await studentService.searchStudents(searchParams);
        if (data?.items) {
          allItems = [...allItems, ...data.items.map(r => ({
            id: r.id,
            name: getLocalized(r.name, i18n.language),
            code: r.studentCode || "",
            type: "student",
            level: r.levelName || "",
          }))];
        }
      }
      setResults(allItems);
    } catch { setResults([]); }
    finally { setResultsLoading(false); }
  }, [effectiveDirType, buildSearchParams, i18n.language]);

  useEffect(() => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => doSearch(searchQuery, filters), 300);
    return () => {
      if (debounceRef.current) clearTimeout(debounceRef.current);
    };
  }, [searchQuery, filters, scopeNode, selectedYearObj, selectedSemesterObj, doSearch]);

  const handleClearFilters = useCallback(() => {
    setSearchQuery("");
    setFilters({});
  }, []);

  const handleSelectEntity = useCallback((entity) => select(entity), [select]);

  const handleTabChange = useCallback((tab) => {
    setActiveTab(tab);
    setFilters({});
    setSearchQuery("");
  }, []);

  const resolveOptions = (filter) => {
    if (filter.options) return filter.options;
    if (filter.key === "role" && filterOptions.roles) return filterOptions.roles;
    if (filter.key === "level" && filterOptions.levels) return filterOptions.levels;
    if (filter.key === "facultyId" && filterOptions.faculties) return filterOptions.faculties;
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

        {activeFilters.map(filter => {
          const options = resolveOptions(filter);
          if (!options || options.length === 0) return null;
          return (
            <div className="sec-filter-group" key={filter.key}>
              <label className="sec-filter-label">{t(filter.labelKey)}</label>
              <div className="sec-filter-select-wrap">
                <select
                  className="sec-filter-select"
                  value={filters[filter.key] || ""}
                  onChange={e => setFilters(prev => ({ ...prev, [filter.key]: e.target.value }))}
                >
                  <option value="">{t("all")}</option>
                  {options.map((opt, i) => (
                    <option key={i} value={opt.value}>{opt.labelKey ? t(opt.labelKey) : opt.label}</option>
                  ))}
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
        <div className="sec-pinned-card">
          <div className="sec-pinned-avatar">
            {selected.name?.charAt(0).toUpperCase()}
          </div>
          <div className="sec-pinned-info">
            <span className="sec-pinned-name">{selected.name}</span>
            <span className="sec-pinned-code">{selected.code}</span>
          </div>
          <span className={`sec-pinned-badge type-${selected.type}`}>
            {selected.type === "staff" ? t("staff") : t("student")}
          </span>
          <button className="sec-pinned-clear" onClick={clear} title={t("clear_selection")}>
            <X size={11} />
          </button>
        </div>
      )}

      {/* ── Results ───────────────────────── */}
      <div className="sec-results">
        <div className="sec-results-header">
          {resultsLoading ? (
            <span className="sec-results-status">{t("searching")}</span>
          ) : (
            <span className="sec-results-status">
              {results.length} {effectiveDirType === "staff" ? t("staff") : effectiveDirType === "student" ? t("students") : t("users")}
            </span>
          )}
          {!resultsLoading && results.length > 0 && (
            <span className="sec-results-hint"><Eye size={10} />{t("select_user_hint")}</span>
          )}
        </div>
        <div className="sec-results-list">
          {results.length === 0 && !resultsLoading && (
            <div className="sec-results-empty">
              {searchQuery || hasActiveFilters ? t("no_results") : <><Search size={20} /><span>{t("search_hint")}</span></>}
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
        </div>
      </div>
    </aside>
  );
}

export default SecondarySidebar;
