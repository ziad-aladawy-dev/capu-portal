import PropTypes from "prop-types";
import { useState, useCallback, useEffect, useRef, Fragment } from "react";
import { useNavigate } from "react-router-dom";
import {
  Search, X, ChevronDown, ChevronRight, Building2,
  CalendarRange, BookOpen, User, SlidersHorizontal, RotateCcw,
} from "lucide-react";
import { useDomain } from "../../contexts/DomainContext";
import { useAcademic } from "../../contexts/AcademicContext";
import { useStickySelection } from "../../contexts/StickySelectionContext";
import * as staffService from "../../services/staffService";
import * as studentService from "../../services/studentService";
import * as structureService from "../../services/structureService";
import * as permissionService from "../../services/permissionService";
import "./secondarySidebar.css";

const DIRECTORY_META = {
  staff: { heading: "Staff Directory", placeholder: "Search staff by name or ID" },
  student: { heading: "Student Directory", placeholder: "Search students by name or ID" },
  all: { heading: "User Directory", placeholder: "Search by name or ID" },
};

function SecondarySidebar({ config, sidebarOpen, sidebarWidth }) {
  const navigate = useNavigate();
  const { selectedDomain } = useDomain();
  const { selectedYear, selectedSemester } = useAcademic();
  const { selected, select, clear, isActive } = useStickySelection();

  const [searchQuery, setSearchQuery] = useState("");
  const [expandedNodes, setExpandedNodes] = useState(new Set());
  const [filters, setFilters] = useState({});
  const [results, setResults] = useState([]);
  const [resultsLoading, setResultsLoading] = useState(false);
  const debounceRef = useRef(null);
  const [structureTree, setStructureTree] = useState([]);
  const [treeLoading, setTreeLoading] = useState(false);
  const [selectedNodeId, setSelectedNodeId] = useState(null);
  const [filterOptions, setFilterOptions] = useState({});

  const dirType = config?.directoryType || "all";
  const meta = DIRECTORY_META[dirType] || DIRECTORY_META.all;
  const configFilters = config?.filters || [];

  // Load structure tree on mount
  useEffect(() => {
    const loadTree = async () => {
      setTreeLoading(true);
      try {
        const roots = await structureService.fetchStructureTree();
        const rootsArray = Array.isArray(roots) ? roots : [];
        setStructureTree(rootsArray);
        if (rootsArray.length > 0) {
          setExpandedNodes(prev => new Set(prev).add(rootsArray[0].id));
        }
      } catch {
        setStructureTree([]);
      } finally {
        setTreeLoading(false);
      }
    };
    loadTree();
  }, []);

  // Load filter options based on directory type
  useEffect(() => {
    const loadFilterOptions = async () => {
      const options = {};
      try {
        if (dirType === "staff" || dirType === "all") {
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
  }, [dirType]);

  // Helper to get dir type display text
  const getDirTypeText = () => {
    if (dirType === "staff") return "staff";
    if (dirType === "student") return "students";
    return "users";
  };

  // Build search parameters from filters
  const buildSearchParams = useCallback((query, activeFilters, nodeId) => {
    const params = {
      search: query || undefined,
      page: 1,
      pageSize: 20,
    };

    if (nodeId) {
      params.structureNodeId = nodeId;
    }

    if (activeFilters) {
      if (activeFilters.structureNodeId) {
        params.structureNodeId = activeFilters.structureNodeId;
      }
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
      if (activeFilters.department) {
        params.structureNodeId = activeFilters.department;
      }
      if (activeFilters.facultyId) {
        params.facultyId = activeFilters.facultyId;
      }
    }
    return params;
  }, []);

  // Perform search with current filters
  const doSearch = useCallback(async (query, activeFilters, nodeId) => {
    const hasActiveFilters = activeFilters && Object.values(activeFilters).some(v => v !== undefined && v !== null && v !== '');
    if (!query && !hasActiveFilters && !nodeId) {
      setResults([]);
      return;
    }
    setResultsLoading(true);
    try {
      const searchParams = buildSearchParams(query, activeFilters, nodeId);
      let allItems = [];
      
      if (dirType === "staff" || dirType === "all") {
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
      if (dirType === "student" || dirType === "all") {
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
  }, [dirType, buildSearchParams]);

  // Debounce search
  useEffect(() => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => {
      doSearch(searchQuery, filters, selectedNodeId);
    }, 300);
    return () => {
      if (debounceRef.current) clearTimeout(debounceRef.current);
    };
  }, [searchQuery, filters, selectedNodeId, doSearch]);

  const toggleNode = useCallback((nodeId) => {
    setExpandedNodes((prev) => {
      const next = new Set(prev);
      if (next.has(nodeId)) next.delete(nodeId);
      else next.add(nodeId);
      return next;
    });
  }, []);

  const handleClearFilters = useCallback(() => {
    setSearchQuery("");
    setFilters({});
    setSelectedNodeId(null);
  }, []);

  const handleSelectEntity = useCallback((entity) => {
    select(entity);
    navigate(`/admin/users/${entity.id}`);
  }, [select, navigate]);

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

  // Render tree node with proper accessibility
  const renderTreeNodeElement = (node) => (
    <div className="sec-tree-node" key={node.id}>
      {node.children && node.children.length > 0 ? (
        <button
          className="sec-tree-toggle"
          onClick={(e) => { e.stopPropagation(); toggleNode(node.id); }}
          aria-label={expandedNodes.has(node.id) ? "Collapse" : "Expand"}
        >
          {expandedNodes.has(node.id) ? <ChevronDown size={10} /> : <ChevronRight size={10} />}
        </button>
      ) : (
        <span className="sec-tree-toggle" style={{ visibility: 'hidden' }}>
          <ChevronRight size={10} />
        </span>
      )}
      <button
        className={selectedNodeId === node.id ? "sec-tree-label active" : "sec-tree-label"}
        onClick={() => {
          setSelectedNodeId(prev => prev === node.id ? null : node.id);
        }}
      >
        {node.name}
      </button>
    </div>
  );

  // Render tree nodes recursively
  const renderTreeNodes = (nodes) => {
    return nodes.map((node) => (
      <Fragment key={node.id}>
        {renderTreeNodeElement(node)}
        {expandedNodes.has(node.id) && node.children && node.children.length > 0 && (
          <div className="sec-tree-children">
            {renderTreeNodes(node.children)}
          </div>
        )}
      </Fragment>
    ));
  };

  // Render tree filter section
  const renderTreeFilter = () => {
    if (treeLoading) {
      return (
        <div className="sec-tree-node">
          <span className="sec-tree-label" style={{ color: 'rgba(26,31,94,0.3)' }}>Loading...</span>
        </div>
      );
    }
    if (structureTree.length === 0) {
      return (
        <div className="sec-tree-node">
          <span className="sec-tree-label" style={{ color: 'rgba(26,31,94,0.3)' }}>No structure data</span>
        </div>
      );
    }
    return renderTreeNodes(structureTree);
  };

  // Get results count text
  const getResultsCountText = () => {
    if (resultsLoading) return "Searching...";
    return `${results.length} ${getDirTypeText()}`;
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
            <strong>{selectedDomain?.name || "All"}</strong>
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

      <div className="sec-sidebar-filters">
        <div className="sec-search-box">
          <Search size={13} />
          <input
            type="text"
            placeholder={meta.placeholder}
            aria-label={meta.placeholder}
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
          />
          {searchQuery && (
            <button className="sec-clear-search" onClick={() => setSearchQuery("")}>
              <X size={12} />
            </button>
          )}
        </div>

        <div className="sec-filter-section">
          <div className="sec-filter-header">
            <SlidersHorizontal size={11} />
            <span>Organization</span>
          </div>
          <div className="sec-tree-filter">
            {renderTreeFilter()}
          </div>
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
            {getResultsCountText()}
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
              className={isActive && selected?.id === entity.id ? "sec-result-item is-selected" : "sec-result-item"}
              onClick={() => handleSelectEntity(entity)}
            >
              <div className={`sec-result-avatar type-${entity.type}`}>
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

      {isActive && (
        <div className="sec-sticky-bar">
          <div className="sec-sticky-info">
            <User size={12} />
            <span>{selected?.name}</span>
          </div>
          <button className="sec-sticky-clear" onClick={clear}>
            <X size={12} />
          </button>
        </div>
      )}
    </aside>
  );
}

SecondarySidebar.propTypes = {
  config: PropTypes.shape({
    directoryType: PropTypes.oneOf(["staff", "student", "all"]),
    filters: PropTypes.arrayOf(PropTypes.shape({
      key: PropTypes.string.isRequired,
      label: PropTypes.string.isRequired,
      options: PropTypes.arrayOf(PropTypes.shape({
        value: PropTypes.string,
        label: PropTypes.string,
      })),
    })),
  }),
  sidebarOpen: PropTypes.bool.isRequired,
  sidebarWidth: PropTypes.number.isRequired,
};

SecondarySidebar.defaultProps = {
  config: { directoryType: "all", filters: [] },
};

export default SecondarySidebar;
