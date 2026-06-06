import { useState, useEffect, useCallback, useRef } from "react";
import { useNavigate } from "react-router-dom";
import {
  GraduationCap, Search, Filter, X, Download, Upload,
  CheckCircle, XCircle, Eye, Edit3, ChevronLeft, ChevronRight
} from "lucide-react";
import userService from "../../users/services/userService";
import { useDomain } from "../../../core/contexts/DomainContext";
import { useAcademic } from "../../../core/contexts/AcademicContext";
import { useToast } from "../../../core/components/Toast";
import { useUserScope } from "../../../core/hooks/useUserScope";
import { SkeletonTable } from "../../../core/components/Skeleton";
import EmptyState from "../../../core/components/EmptyState";
import UserDetailView from "../../../core/components/UserDetailView";
import "../../users/styles/users.css";
import "../../users/styles/userTable.css";

const FILTER_DEFAULTS = {
  search: "",
  isActive: "",
  passwordExpired: "",
  facultyId: "",
  programId: "",
  levelId: "",
};

function StudentDirectory() {
  const navigate = useNavigate();
  const { addToast } = useToast();
  const { scopeNode } = useDomain();
  const { selectedYearObj, selectedSemesterObj } = useAcademic();
  const { scopedUser, isScoped, clearScope, scopeToUser } = useUserScope();
  const searchRef = useRef(null);

  if (isScoped && scopedUser?.type === "student") {
    return <UserDetailView userId={scopedUser.id} userType="student" onBack={clearScope} />;
  }

  const [students, setStudents] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [pagination, setPagination] = useState({ pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 1 });
  const [filters, setFilters] = useState(FILTER_DEFAULTS);
  const [appliedFilters, setAppliedFilters] = useState({});
  const [showAdvanced, setShowAdvanced] = useState(false);
  const [selectedIds, setSelectedIds] = useState(new Set());
  const [faculties, setFaculties] = useState([]);
  const [programs, setPrograms] = useState([]);
  const [levels, setLevels] = useState([]);
  const [showExportMenu, setShowExportMenu] = useState(false);
  const exportRef = useRef(null);

  useEffect(() => {
    userService.getFaculties().then(setFaculties).catch(() => {});
  }, []);

  const loadStudents = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params = {
        Page: pagination.pageNumber,
        PageSize: pagination.pageSize,
        ScopeNodeId: scopeNode?.id || undefined,
        AcademicYearId: selectedYearObj?.id || undefined,
        SemesterId: selectedSemesterObj?.id || undefined,
        Search: appliedFilters.search || undefined,
        IsActive: appliedFilters.isActive !== "" ? appliedFilters.isActive === "true" : undefined,
        PasswordExpired: appliedFilters.passwordExpired !== "" ? appliedFilters.passwordExpired === "true" : undefined,
        FacultyId: appliedFilters.facultyId || undefined,
        ProgramId: appliedFilters.programId || undefined,
        LevelId: appliedFilters.levelId || undefined,
      };
      const data = await userService.getAllStudents(params);
      setStudents(data.items || []);
      setPagination(prev => ({
        ...prev,
        totalCount: data.totalCount || 0,
        totalPages: data.totalPages || 1,
      }));
    } catch (err) {
      setError(err.message || "Failed to load students");
      setStudents([]);
    } finally {
      setLoading(false);
    }
  }, [pagination.pageNumber, pagination.pageSize, appliedFilters, scopeNode, selectedYearObj, selectedSemesterObj]);

  useEffect(() => { loadStudents(); }, [loadStudents]);

  const handleFilterChange = (e) => {
    const { name, value } = e.target;
    setFilters(prev => ({ ...prev, [name]: value }));
    if (name === "facultyId") {
      setFilters(prev => ({ ...prev, programId: "", levelId: "" }));
      if (value) userService.getPrograms(value).then(setPrograms).catch(() => setPrograms([]));
      else { setPrograms([]); setLevels([]); }
    }
    if (name === "programId") {
      setFilters(prev => ({ ...prev, levelId: "" }));
      if (value) userService.getLevels(value).then(setLevels).catch(() => setLevels([]));
      else setLevels([]);
    }
  };

  const applyFilters = () => {
    setAppliedFilters({ ...filters });
    setPagination(prev => ({ ...prev, pageNumber: 1 }));
    setSelectedIds(new Set());
  };

  const resetFilters = () => {
    setFilters(FILTER_DEFAULTS);
    setAppliedFilters({});
    setPrograms([]);
    setLevels([]);
    setPagination(prev => ({ ...prev, pageNumber: 1 }));
    setSelectedIds(new Set());
  };

  const handleSearchKeyDown = (e) => {
    if (e.key === "Enter") applyFilters();
  };

  const clearSearch = () => {
    setFilters(prev => ({ ...prev, search: "" }));
    setAppliedFilters(prev => {
      const next = { ...prev };
      delete next.search;
      return next;
    });
  };

  const handleSelectAll = () => {
    if (students.every(s => selectedIds.has(s.id))) {
      setSelectedIds(new Set());
    } else {
      setSelectedIds(new Set(students.map(s => s.id)));
    }
  };

  const handleSelectOne = (id) => {
    setSelectedIds(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const handleBulkAction = async (action) => {
    const ids = Array.from(selectedIds);
    if (!ids.length) return;
    if (action === "delete" && !window.confirm(`Delete ${ids.length} student(s)? This cannot be undone.`)) return;

    let result;
    try {
      if (action === "activate") result = await userService.bulkActivateUsers(ids, "Student");
      else if (action === "deactivate") result = await userService.bulkDeactivateUsers(ids, "Student");
      else if (action === "delete") result = await userService.bulkDeleteUsers(ids, "Student");
      if (result?.success) {
        addToast(`${ids.length} student(s) ${action === "activate" ? "activated" : action === "deactivate" ? "deactivated" : "deleted"}`, "success");
        setSelectedIds(new Set());
        loadStudents();
      }
    } catch (err) {
      addToast(`Bulk ${action} failed: ${err.message}`, "error");
    }
  };

  const handleExport = async (format) => {
    setShowExportMenu(false);
    const baseParams = {
      ScopeNodeId: scopeNode?.id || undefined,
      AcademicYearId: selectedYearObj?.id || undefined,
      SemesterId: selectedSemesterObj?.id || undefined,
      Search: appliedFilters.search || undefined,
      IsActive: appliedFilters.isActive !== "" ? appliedFilters.isActive === "true" : undefined,
      PasswordExpired: appliedFilters.passwordExpired !== "" ? appliedFilters.passwordExpired === "true" : undefined,
    };
    try {
      const blob = format === "csv"
        ? await userService.exportStudentsCsv(baseParams)
        : await userService.exportStudentsExcel(baseParams);
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `students_${new Date().toISOString().slice(0, 10)}.${format === "csv" ? "csv" : "xlsx"}`;
      document.body.appendChild(a);
      a.click();
      a.remove();
      window.URL.revokeObjectURL(url);
      addToast("Export completed", "success");
    } catch (err) {
      addToast(`Export failed: ${err.message}`, "error");
    }
  };

  const allSelected = students.length > 0 && students.every(s => selectedIds.has(s.id));
  const someSelected = students.some(s => selectedIds.has(s.id));
  const firstItem = pagination.totalCount === 0 ? 0 : (pagination.pageNumber - 1) * pagination.pageSize + 1;
  const lastItem = Math.min(pagination.pageNumber * pagination.pageSize, pagination.totalCount);

  return (
    <div className="users-page">
      <div className="users-page-header">
        <div className="users-page-title">
          <div className="users-page-icon"><GraduationCap size={18} /></div>
          <div>
            <span className="users-page-kicker">People Management</span>
            <h1>Student Directory</h1>
            <p>Manage and view all students</p>
          </div>
        </div>
        <div className="users-page-actions">
          <button className="users-secondary-btn" onClick={() => setShowExportMenu(true)} ref={exportRef}>
            <Download size={15} /> Export
          </button>
          <button className="users-primary-btn" onClick={() => navigate("/admin/users/add-student")}>
            <Upload size={15} /> Add Student
          </button>
        </div>
      </div>

      {showExportMenu && (
        <>
          <div className="users-export-backdrop" onClick={() => setShowExportMenu(false)} />
          <div className="users-export-menu" style={{ position: "fixed", top: exportRef.current?.getBoundingClientRect().bottom + 6, right: 20, zIndex: 1000 }}>
            <button onClick={() => handleExport("excel")}>Excel (.xlsx)</button>
            <button onClick={() => handleExport("csv")}>CSV (.csv)</button>
          </div>
        </>
      )}

      <section className="users-filter-card">
        <div className="users-filter-row">
          <div className="users-search-box">
            <Search size={17} className="users-search-icon" />
            <input
              ref={searchRef}
              type="text"
              name="search"
              value={filters.search}
              onChange={handleFilterChange}
              onKeyDown={handleSearchKeyDown}
              placeholder="Search by name, email, code, or national ID..."
            />
            {filters.search && (
              <button type="button" className="users-clear-search" onClick={clearSearch}>
                <X size={14} />
              </button>
            )}
          </div>
          <button type="button" className="users-filter-btn primary" onClick={applyFilters}>
            Search
          </button>
          <button type="button" className={`users-filter-btn soft ${showAdvanced ? "active" : ""}`} onClick={() => setShowAdvanced(!showAdvanced)}>
            <Filter size={16} /> Filters
          </button>
        </div>

        {showAdvanced && (
          <div className="users-advanced-panel">
            <div className="users-filter-grid">
              <div className="users-filter-field">
                <label>Status</label>
                <select name="isActive" value={filters.isActive} onChange={handleFilterChange}>
                  <option value="">All</option>
                  <option value="true">Active</option>
                  <option value="false">Inactive</option>
                </select>
              </div>
              <div className="users-filter-field">
                <label>Password Status</label>
                <select name="passwordExpired" value={filters.passwordExpired} onChange={handleFilterChange}>
                  <option value="">All</option>
                  <option value="false">Valid</option>
                  <option value="true">Expired</option>
                </select>
              </div>
              <div className="users-filter-field">
                <label>Faculty</label>
                <select name="facultyId" value={filters.facultyId} onChange={handleFilterChange}>
                  <option value="">All Faculties</option>
                  {faculties.map(f => <option key={f.id} value={f.id}>{f.name}</option>)}
                </select>
              </div>
              <div className="users-filter-field">
                <label>Program</label>
                <select name="programId" value={filters.programId} onChange={handleFilterChange} disabled={!filters.facultyId}>
                  <option value="">All Programs</option>
                  {programs.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}
                </select>
              </div>
              <div className="users-filter-field">
                <label>Level</label>
                <select name="levelId" value={filters.levelId} onChange={handleFilterChange} disabled={!filters.programId}>
                  <option value="">All Levels</option>
                  {levels.map(l => <option key={l.id} value={l.id}>{l.name}</option>)}
                </select>
              </div>
            </div>
            <div className="users-filter-actions">
              <button type="button" className="users-filter-btn soft" onClick={resetFilters}>Reset</button>
              <button type="button" className="users-filter-btn gold" onClick={applyFilters}>Apply Filters</button>
            </div>
          </div>
        )}
      </section>

      <div className="users-table-section">
        <div className="table-container">
          {loading ? (
            <SkeletonTable rows={8} columns={7} />
          ) : error ? (
            <div className="table-container error-state" style={{ padding: "40px 18px" }}>
              <p style={{ color: "#dc2626", fontWeight: 600, fontSize: 13 }}>Failed to load students</p>
              <p style={{ color: "#6b7280", fontSize: 12, marginTop: 4 }}>{error}</p>
              <button className="users-filter-btn gold" style={{ marginTop: 12 }} onClick={loadStudents}>Retry</button>
            </div>
          ) : students.length === 0 ? (
            <EmptyState
              icon={GraduationCap}
              title="No students found"
              message={Object.keys(appliedFilters).length ? "Try adjusting your search or filters" : "Add your first student to get started"}
              actionLabel={Object.keys(appliedFilters).length ? "Clear Filters" : "Add Student"}
              onAction={Object.keys(appliedFilters).length ? resetFilters : () => navigate("/admin/users/add-student")}
            />
          ) : (
            <table className="users-table">
              <thead>
                <tr>
                  <th className="bulk-check-cell">
                    <input type="checkbox" className="bulk-checkbox" ref={el => { if (el) el.indeterminate = someSelected && !allSelected; }} checked={allSelected} onChange={handleSelectAll} />
                  </th>
                  <th>#</th>
                  <th>Code</th>
                  <th>Name</th>
                  <th>Email</th>
                  <th>Status</th>
                  <th>Password</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {students.map((s, idx) => (
                  <tr key={s.id} className={selectedIds.has(s.id) ? "selected-row" : ""} style={{ cursor: "pointer" }} onClick={() => scopeToUser({ id: s.id, name: s.name, code: s.studentCode, type: "student" })}>
                    <td className="bulk-check-cell" onClick={e => e.stopPropagation()}>
                      <input type="checkbox" className="bulk-checkbox" checked={selectedIds.has(s.id)} onChange={() => handleSelectOne(s.id)} />
                    </td>
                    <td>{(pagination.pageNumber - 1) * pagination.pageSize + idx + 1}</td>
                    <td style={{ fontFamily: "Space Mono, monospace", fontWeight: 700 }}>{s.studentCode}</td>
                    <td style={{ fontWeight: 600 }}>{s.name}</td>
                    <td style={{ color: "#6b7280", fontSize: 11 }}>{s.email}</td>
                    <td>
                      <span className={`status-badge ${s.isActive ? "status-active" : "status-inactive"}`}>
                        <span className="status-dot" />
                        {s.isActive ? "Active" : "Inactive"}
                      </span>
                    </td>
                    <td>
                      <span className={`password-badge ${s.passwordStatus === "Expired" ? "password-expired" : "password-valid"}`}>
                        {s.passwordStatus || "Valid"}
                      </span>
                    </td>
                    <td onClick={e => e.stopPropagation()}>
                      <div className="action-buttons">
                        <button className="action-btn info-btn" onClick={() => scopeToUser({ id: s.id, name: s.name, code: s.studentCode, type: "student" })} title="View Details">
                          <Eye size={16} />
                        </button>
                        <button className="action-btn edit-btn" onClick={() => navigate(`/admin/users/edit-student/${s.id}`)} title="Edit">
                          <Edit3 size={16} />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}

          {!loading && !error && students.length > 0 && pagination.totalPages > 1 && (
            <div className="pagination-container">
              <button className="pagination-btn" disabled={pagination.pageNumber <= 1} onClick={() => setPagination(prev => ({ ...prev, pageNumber: prev.pageNumber - 1 }))}>
                <ChevronLeft size={14} />
              </button>
              {Array.from({ length: Math.min(pagination.totalPages, 7) }, (_, i) => {
                let pageNum;
                if (pagination.totalPages <= 7) {
                  pageNum = i + 1;
                } else {
                  const mid = Math.floor(7 / 2);
                  if (pagination.pageNumber <= mid + 1) pageNum = i + 1;
                  else if (pagination.pageNumber >= pagination.totalPages - mid) pageNum = pagination.totalPages - 7 + i + 1;
                  else pageNum = pagination.pageNumber - mid + i;
                }
                return (
                  <button key={pageNum} className={`pagination-btn ${pageNum === pagination.pageNumber ? "active" : ""}`} onClick={() => setPagination(prev => ({ ...prev, pageNumber: pageNum }))}>
                    {pageNum}
                  </button>
                );
              })}
              <button className="pagination-btn" disabled={pagination.pageNumber >= pagination.totalPages} onClick={() => setPagination(prev => ({ ...prev, pageNumber: prev.pageNumber + 1 }))}>
                <ChevronRight size={14} />
              </button>
            </div>
          )}
        </div>
      </div>

      <div className="users-pagination-footer">
        <div className="page-size-control">
          <label>Show</label>
          <select value={pagination.pageSize} onChange={e => setPagination({ pageNumber: 1, pageSize: parseInt(e.target.value), totalCount: 0, totalPages: 1 })}>
            <option value="10">10</option>
            <option value="20">20</option>
            <option value="50">50</option>
            <option value="100">100</option>
          </select>
          <span>entries per page</span>
        </div>
        <div className="page-results-text">Showing {firstItem}–{lastItem} of {pagination.totalCount} results</div>
      </div>

      {selectedIds.size > 0 && (
        <div className="bulk-action-bar">
          <span className="bulk-action-count">{selectedIds.size} selected</span>
          <div className="bulk-action-buttons">
            <button className="bulk-action-btn activate" onClick={() => handleBulkAction("activate")}>
              <CheckCircle size={14} /> Activate
            </button>
            <button className="bulk-action-btn deactivate" onClick={() => handleBulkAction("deactivate")}>
              <XCircle size={14} /> Deactivate
            </button>
            <button className="bulk-action-btn export" onClick={() => handleExport("excel")}>
              <Download size={14} /> Export
            </button>
            <button className="bulk-action-btn delete" onClick={() => handleBulkAction("delete")}>
              <XCircle size={14} /> Delete
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

export default StudentDirectory;
