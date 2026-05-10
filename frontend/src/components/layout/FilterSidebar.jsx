import React from "react";
import { ChevronRight, X, SlidersHorizontal, Users, GraduationCap, DollarSign, BookOpen, Shield } from "lucide-react";
import { useFilters } from "../../hooks/use-filters";
import { useScope } from "../../hooks/use-scope";
import { FILTER_CATEGORIES } from "../../lib/constants";
import "./FilterSidebar.css";

const CategoryFilters = {
  [FILTER_CATEGORIES.STUDENTS]: () => {
    const { getActiveFilters, updateFilter, clearCurrentCategoryFilters } = useFilters();
    const { getFacultyOptions, getProgramOptions } = useScope();
    const filters = getActiveFilters();
    
    const facultyOptions = getFacultyOptions();
    const programOptions = getProgramOptions(filters.collegeId);
    
    return (
      <>
        <div className="filter-group">
          <label className="filter-label">Faculty / College</label>
          <select 
            className="filter-select"
            value={filters.collegeId || ""}
            onChange={(e) => updateFilter("collegeId", e.target.value || null)}
          >
            <option value="">All Faculties</option>
            {facultyOptions.map(f => (
              <option key={f.id} value={f.id}>{f.name}</option>
            ))}
          </select>
        </div>
        
        <div className="filter-group">
          <label className="filter-label">Program</label>
          <select 
            className="filter-select"
            value={filters.programId || ""}
            onChange={(e) => updateFilter("programId", e.target.value || null)}
          >
            <option value="">All Programs</option>
            {programOptions.map(p => (
              <option key={p.id} value={p.id}>{p.name}</option>
            ))}
          </select>
        </div>
        
        <div className="filter-group">
          <label className="filter-label">Enrollment Status</label>
          <select 
            className="filter-select"
            value={filters.status || ""}
            onChange={(e) => updateFilter("status", e.target.value || null)}
          >
            <option value="">All Statuses</option>
            <option value="Active">Active</option>
            <option value="Inactive">Inactive</option>
            <option value="Graduated">Graduated</option>
            <option value="Suspended">Suspended</option>
          </select>
        </div>
        
        <div className="filter-group">
          <label className="filter-label">Academic Year</label>
          <select 
            className="filter-select"
            value={filters.academicYearId || ""}
            onChange={(e) => updateFilter("academicYearId", e.target.value || null)}
          >
            <option value="">All Years</option>
            <option value="year-2026">2026</option>
            <option value="year-2025">2025</option>
            <option value="year-2024">2024</option>
          </select>
        </div>
        
        <div className="filter-actions">
          <button className="clear-filters-btn" onClick={clearCurrentCategoryFilters}>
            <X size={16} />
            Clear Filters
          </button>
        </div>
      </>
    );
  },
  
  [FILTER_CATEGORIES.ADMIN]: () => {
    const { getActiveFilters, updateFilter, clearCurrentCategoryFilters } = useFilters();
    const filters = getActiveFilters();
    
    return (
      <>
        <div className="filter-group">
          <label className="filter-label">User Type</label>
          <select 
            className="filter-select"
            value={filters.userType || ""}
            onChange={(e) => updateFilter("userType", e.target.value || null)}
          >
            <option value="">All Types</option>
            <option value="admin">Administrator</option>
            <option value="instructor">Instructor</option>
            <option value="staff">Staff</option>
            <option value="registrar">Registrar</option>
            <option value="financial">Financial Officer</option>
          </select>
        </div>
        
        <div className="filter-group">
          <label className="filter-label">Role</label>
          <select 
            className="filter-select"
            value={filters.roleId || ""}
            onChange={(e) => updateFilter("roleId", e.target.value || null)}
          >
            <option value="">All Roles</option>
            <option value="role-super-admin">Super Admin</option>
            <option value="role-college-admin">College Admin</option>
            <option value="role-registrar">Registrar</option>
            <option value="role-financial">Financial Officer</option>
            <option value="role-faculty">Faculty</option>
          </select>
        </div>
        
        <div className="filter-group">
          <label className="filter-label">Status</label>
          <select 
            className="filter-select"
            value={filters.userStatus || ""}
            onChange={(e) => updateFilter("userStatus", e.target.value || null)}
          >
            <option value="">All Statuses</option>
            <option value="active">Active</option>
            <option value="inactive">Inactive</option>
            <option value="suspended">Suspended</option>
          </select>
        </div>
        
        <div className="filter-group">
          <label className="filter-label">Department</label>
          <select 
            className="filter-select"
            value={filters.departmentId || ""}
            onChange={(e) => updateFilter("departmentId", e.target.value || null)}
          >
            <option value="">All Departments</option>
            <option value="dept-cs">Computer Science</option>
            <option value="dept-eng">Engineering</option>
            <option value="dept-bus">Business</option>
            <option value="dept-arts">Liberal Arts</option>
          </select>
        </div>
        
        <div className="filter-actions">
          <button className="clear-filters-btn" onClick={clearCurrentCategoryFilters}>
            <X size={16} />
            Clear Filters
          </button>
        </div>
      </>
    );
  },
  
  [FILTER_CATEGORIES.FINANCIAL]: () => {
    const { getActiveFilters, updateFilter, clearCurrentCategoryFilters } = useFilters();
    const filters = getActiveFilters();
    
    return (
      <>
        <div className="filter-group">
          <label className="filter-label">Payment Status</label>
          <select 
            className="filter-select"
            value={filters.paymentStatus || ""}
            onChange={(e) => updateFilter("paymentStatus", e.target.value || null)}
          >
            <option value="">All Statuses</option>
            <option value="paid">Paid</option>
            <option value="pending">Pending</option>
            <option value="partial">Partial</option>
            <option value="overdue">Overdue</option>
          </select>
        </div>
        
        <div className="filter-group">
          <label className="filter-label">Transaction Type</label>
          <select 
            className="filter-select"
            value={filters.transactionType || ""}
            onChange={(e) => updateFilter("transactionType", e.target.value || null)}
          >
            <option value="">All Types</option>
            <option value="tuition">Tuition</option>
            <option value="fees">Fees</option>
            <option value="scholarship">Scholarship</option>
            <option value="fine">Fine</option>
          </select>
        </div>
        
        <div className="filter-group">
          <label className="filter-label">Academic Year</label>
          <select 
            className="filter-select"
            value={filters.academicYearId || ""}
            onChange={(e) => updateFilter("academicYearId", e.target.value || null)}
          >
            <option value="">All Years</option>
            <option value="year-2026">2026</option>
            <option value="year-2025">2025</option>
            <option value="year-2024">2024</option>
          </select>
        </div>
        
        <div className="filter-actions">
          <button className="clear-filters-btn" onClick={clearCurrentCategoryFilters}>
            <X size={16} />
            Clear Filters
          </button>
        </div>
      </>
    );
  },
  
  [FILTER_CATEGORIES.REGISTRATION]: () => {
    const { getActiveFilters, updateFilter, clearCurrentCategoryFilters } = useFilters();
    const filters = getActiveFilters();
    
    return (
      <>
        <div className="filter-group">
          <label className="filter-label">Registration Status</label>
          <select 
            className="filter-select"
            value={filters.registrationStatus || ""}
            onChange={(e) => updateFilter("registrationStatus", e.target.value || null)}
          >
            <option value="">All Statuses</option>
            <option value="open">Open</option>
            <option value="closed">Closed</option>
            <option value="in-progress">In Progress</option>
          </select>
        </div>
        
        <div className="filter-group">
          <label className="filter-label">Semester</label>
          <select 
            className="filter-select"
            value={filters.semesterId || ""}
            onChange={(e) => updateFilter("semesterId", e.target.value || null)}
          >
            <option value="">All Semesters</option>
            <option value="sem-fall">Fall</option>
            <option value="sem-spring">Spring</option>
            <option value="sem-summer">Summer</option>
          </select>
        </div>
        
        <div className="filter-group">
          <label className="filter-label">Academic Year</label>
          <select 
            className="filter-select"
            value={filters.academicYearId || ""}
            onChange={(e) => updateFilter("academicYearId", e.target.value || null)}
          >
            <option value="">All Years</option>
            <option value="year-2026">2026</option>
            <option value="year-2025">2025</option>
            <option value="year-2024">2024</option>
          </select>
        </div>
        
        <div className="filter-actions">
          <button className="clear-filters-btn" onClick={clearCurrentCategoryFilters}>
            <X size={16} />
            Clear Filters
          </button>
        </div>
      </>
    );
  }
};

const CategoryLabels = {
  [FILTER_CATEGORIES.STUDENTS]: "Students",
  [FILTER_CATEGORIES.ADMIN]: "Admin",
  [FILTER_CATEGORIES.FINANCIAL]: "Financial",
  [FILTER_CATEGORIES.REGISTRATION]: "Registration"
};

const CategoryIcons = {
  [FILTER_CATEGORIES.STUDENTS]: GraduationCap,
  [FILTER_CATEGORIES.ADMIN]: Shield,
  [FILTER_CATEGORIES.FINANCIAL]: DollarSign,
  [FILTER_CATEGORIES.REGISTRATION]: BookOpen
};

export const FilterSidebar = ({ isVisible, isOpen = true, onToggle }) => {
  const { currentCategory } = useFilters();
  
  if (!isVisible) return null;
  
  const CategoryFilterComponent = CategoryFilters[currentCategory] || CategoryFilters[FILTER_CATEGORIES.STUDENTS];
  const IconComponent = CategoryIcons[currentCategory] || Users;
  const label = CategoryLabels[currentCategory] || "Filter";
  
  return (
    <aside className={`filter-sidebar ${isOpen ? "expanded" : "collapsed"}`}>
      {/* Header */}
      <div className="filter-header">
        {isOpen && (
          <div className="filter-header-content">
            <IconComponent size={18} />
            <h3 className="filter-title">{label} Filters</h3>
          </div>
        )}
        <button 
          className="filter-toggle"
          onClick={onToggle}
          title={isOpen ? "Collapse" : "Expand"}
        >
          {isOpen ? <ChevronRight size={18} /> : <SlidersHorizontal size={18} />}
        </button>
      </div>
      
      {isOpen && (
        <div className="filter-content">
          <CategoryFilterComponent />
        </div>
      )}
    </aside>
  );
};