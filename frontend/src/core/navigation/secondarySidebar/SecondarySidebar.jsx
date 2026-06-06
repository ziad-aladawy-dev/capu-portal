import { useState, useCallback, useEffect, useRef } from "react";
import {
  Search, X, Building2,
  CalendarRange, BookOpen, User, SlidersHorizontal, RotateCcw,
} from "lucide-react";
import { useDomain } from "../../contexts/DomainContext";
import { useAcademic } from "../../contexts/AcademicContext";
import { useStickySelection } from "../../contexts/StickySelectionContext";
import * as staffService from "../../services/staffService";
import * as studentService from "../../services/studentService";
import * as structureService from "../../services/structureService";
import * as permissionService from "../../services/permissionService";
import { PAGE_TYPES, APPLICABLE_TO } from "../../manifests/manifestTypes";
import "./secondarySidebar.css";

const DIRECTORY_META = {
  staff: { heading: "Staff Directory", placeholder: "Search staff by name or ID" },
  student: { heading: "Student Directory", placeholder: "Search students by name or ID" },
  all: { heading: "User Directory", placeholder: "Search by name or ID" },
};

const DIRECTORY_FILTERS = {
  staff: [
    { key: "role", label: "Role" },
    { key: "status", label: "Status", options: [
      { value: "active", label: "Active" },
      { value: "inactive", label: "Inactive" },
    ]},
    { key: "facultyId", label: "Faculty" },
  ],
  student: [
    { key: "level", label: "Level" },
    { key: "status", label: "Status", options: [
      { value: "active", label: "Active" },
      { value: "inactive", label: "Inactive" },
    ]},
    { key: "facultyId", label: "Faculty" },
    { key: "enrollment", label: "Enrollment", options: [
      { value: "active", label: "Currently Enrolled" },
      { value: "graduated", label: "Graduated" },
    ]},
  ],
  all: [
    { key: "role", label: "Role" },
    { key: "level", label: "Level" },
    { key: "status", label: "Status", options: [
      { value: "active", label: "Active" },
      { value: "inactive", label: "Inactive" },
    ]},
    { key: "facultyId", label: "Faculty" },
  ],
};

function SecondarySidebar({ config, sidebarOpen, sidebarWidth }) {
  const { scopeNode } = useDomain();
  const { selectedYear, selectedSemester, selectedYearObj, selectedSemesterObj } = useAcademic();
  const { selected, select, clear, isActive } = useStickySelection();

  const [searchQuery, setSearchQuery] = useState("");
  const [filters, setFilters] = useState({});
  const [results, setResults] = useState([]);
  const [resultsLoading, setResultsLoading] = useState(false);
  const debounceRef = useRef(null);
  const [filterOptions, setFilterOptions] = useState({});

  const { currentPageType = PAGE_TYPES.MANAGEMENT, currentApplicableTo = APPLICABLE_TO.BOTH } = config || {};

  const dirType = config?.directoryType || (
    currentApplicableTo === APPLICABLE_TO.STAFF ? "staff"
    : currentApplicableTo === APPLICABLE_TO.STUDENT ? "student"
    : "all"
  );

  const [typeFilter, setTypeFilter] = useState("all");

  useEffect(() => {
    if (isActive && selected?.type && dirType === "all") {
      setTypeFilter(selected.type);
    } else if (!isActive && dirType === "all") {
      setTypeFilter("all");
    }
  }, [isActive, selected, dirType]);

  const effectiveDirType = dirType === "all" && typeFilter !== "all" ? typeFilter : dirType;
  const meta = DIRECTORY_META[effectiveDirType] || DIRECTORY_META.all;
  const configFilters = (config?.filters && config.filters.length > 0)
    ? config.filters
    : (DIRECTORY_FILTERS[effectiveDirType] || []);

  useEffect(() => {
    const loadFilterOptions = async () => {
      const options = {};
      try {
        if (effectiveDirType === "staff" || effectiveDirType === "all") {
          const roles = await permissionService.fetchAllRoles({ pageSize: 100 });
          options.roles = (roles?.items || []).map(r => ({ value: r.id, label: r.name }));
        }
        const levels = await structureService.fetchLevels();
        options.levels = (levels || []).map(l => ({ value: l.id, label: l.name }));
        const faculties = await structureService.fetchFaculties();
        options.faculties = (faculties || []).map(f => ({ value: f.id, label: f.name }));
      } catch {
      }
      setFilterOptions(options);
    };
    loadFilterOptions();
  }, [effectiveDirType]);

  const buildSearchParams = useCallback((query, activeFilters) => {
    const params = {
      search: query || undefined,
      page: 1,
      pageSize: 20,
      ScopeNodeId: scopeNode?.id || undefined,
      AcademicYearId: selectedYearObj?.id || undefined,
      SemesterId: selectedSemesterObj?.id || undefined,
    };

    if (activeFilters) {
      if (activeFilters.role) {
        params.role = activeFilters.role;
      }
      if (activeFilters.status === "active") {
        params.isActive = true;
      } else if (activeFilters.status === "inactive") {
        params.isActive = false;
      }
      if (activeFilters.level) {
        params.levelId = activeFilters.level;
      }
      if (activeFilters.enrollment === "graduated") {
        params.isActive = false;
      }
      if (activeFilters.facultyId) {
        params.facultyId = activeFilters.facultyId;
      }
    }
    return params;
  }, [scopeNode, selectedYearObj, selectedSemesterObj]);

  const doSearch = useCallback(async (query, activeFilters) => {
    setResultsLoading(true);
    try {
      const searchParams = buildSearchParams(query, activeFilters);
      let allItems = [];
      if (effectiveDirType === "staff" || effectiveDirType === "all") {
        const data = await staffService.searchStaff(searchParams);
        if (data?.items) {
          allItems = [...allItems, ...data.items.map(r => ({
            id: r.id,
            name: r.name,
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
            name: r.name,
            code: r.studentCode || "",
            type: "student",
            level: r.levelName || "",
          }))];
        }
      }
      setResults(allItems);
    } catch {
      setResults([]);
    } finally {
      setResultsLoading(false);
    }
  }, [effectiveDirType, buildSearchParams]);

  useEffect(() => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => {
      doSearch(searchQuery, filters);
    }, 300);
    return () => {
      if (debounceRef.current) clearTimeout(debounceRef.current);
    };
  }, [searchQuery, filters, scopeNode, selectedYearObj, selectedSemesterObj, doSearch]);

  const handleClearFilters = useCallback(() => {
    setSearchQuery("");
    setFilters({});
  }, []);

  const handleSelectEntity = useCallback((entity) => {
    select(entity);
  }, [select]);

  const resolveOptions = (filter) => {
    if (filter.options) {
      return filter.options;
    }
    if (filter.key === "role" && filterOptions.roles) {
      return filterOptions.roles;
    }
    if (filter.key === "level" && filterOptions.levels) {
      return filterOptions.levels;
    }
    if (filter.key === "facultyId" && filterOptions.faculties) {
      return filterOptions.faculties;
    }
    return filter.options || [];
  };

  return (
    <aside
      className="secondary-sidebar"
      style={{ left: sidebarOpen ? sidebarWidth : 0 }}
    >
      <div className="sec-dir-header">
        <span className="sec-dir-label">{meta.heading}</span>
      </div>

      <div className="sec-sidebar-scope">
        <div className="sec-scope-item">
          <Building2 size={13} />
          <div className="sec-scope-content">
            <span className="sec-scope-label">Scope</span>
            <strong>{scopeNode?.name || "All"}</strong>
          </div>
        </div>
        <div className="sec-scope-row">
          <div className="sec-scope-item small">
            <CalendarRange size={11} />
            <span>{selectedYear}</span>
          </div>
          <div className="sec-scope-item small">
            <BookOpen size={11} />
            <span>{selectedSemester}</span>
          </div>
        </div>
      </div>

      {/* Sticky pinned card — top of filters area */}
      {isActive && (
        <div className="sec-sticky-card">
          <div className={`sec-sticky-card-avatar type-${selected.type}`}>
            {selected.name?.charAt(0).toUpperCase()}
          </div>
          <div className="sec-sticky-card-info">
            <strong>{selected.name}</strong>
            <span>{selected.code}</span>
          </div>
          <span className={`sec-sticky-card-badge type-${selected.type}`}>
            {selected.type === "staff" ? "Staff" : "Student"}
          </span>
          <button className="sec-sticky-card-clear" onClick={clear} title="Clear selection">
            <X size={12} />
          </button>
        </div>
      )}

      <div className="sec-sidebar-filters">
        {/* User-type filter for "both" pages */}
        {dirType === "all" && (
          <div className="sec-filter-section">
            <div className="sec-filter-header">
              <User size={11} />
              <span>Show</span>
            </div>
            <select
              className="sec-filter-select"
              value={typeFilter}
              onChange={(e) => {
                setTypeFilter(e.target.value);
                setFilters({});
                setSearchQuery("");
              }}
            >
              <option value="all">All Users</option>
              <option value="staff">Staff Only</option>
              <option value="student">Students Only</option>
            </select>
          </div>
        )}

        <div className="sec-search-box">
          <Search size={13} />
          <input
            type="text"
            placeholder={meta.placeholder}
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
          />
          {searchQuery && (
            <button className="sec-clear-search" onClick={() => setSearchQuery("")}>
              <X size={12} />
            </button>
          )}
        </div>

        {configFilters.map((filter) => {
          const options = resolveOptions(filter);
          if (!options || options.length === 0) return null;
          return (
            <div className="sec-filter-section" key={filter.key}>
              <div className="sec-filter-header">
                <SlidersHorizontal size={11} />
                <span>{filter.label}</span>
              </div>
              <select
                className="sec-filter-select"
                value={filters[filter.key] || ""}
                onChange={(e) => setFilters((prev) => ({ ...prev, [filter.key]: e.target.value }))}
              >
                <option value="">All {filter.label}</option>
                {options.map((opt) => (
                  <option key={opt.value} value={opt.value}>{opt.label}</option>
                ))}
              </select>
            </div>
          );
        })}

        <button className="sec-clear-all" onClick={handleClearFilters}>
          <RotateCcw size={11} />
          Clear Filters
        </button>
      </div>

      <div className="sec-sidebar-results">
        <div className="sec-results-header">
          <span className="sec-results-count">
            {resultsLoading ? "Searching..." : `${results.length} ${effectiveDirType === "staff" ? "staff" : effectiveDirType === "student" ? "students" : "users"}`}
          </span>
        </div>
        <div className="sec-results-list">
          {results.length === 0 && !resultsLoading && (
            <div className="sec-no-results">No results match your criteria.</div>
          )}
          {resultsLoading && (
            <div className="sec-no-results">Loading...</div>
          )}
          {results.map((entity) => (
            <button
              key={entity.id}
              className={"sec-result-item" + (isActive && selected?.id === entity.id ? " is-selected" : "")}
              onClick={() => handleSelectEntity(entity)}
            >
              <div className={"sec-result-avatar type-" + entity.type}>
                {entity.name.charAt(0).toUpperCase()}
              </div>
              <div className="sec-result-info">
                <strong>{entity.name}</strong>
                <span>{entity.code}</span>
                {entity.role && <small className="sec-result-meta">{entity.role}</small>}
                {entity.level && <small className="sec-result-meta">{entity.level}</small>}
              </div>
            </button>
          ))}
        </div>
      </div>
    </aside>
  );
}

export default SecondarySidebar;
