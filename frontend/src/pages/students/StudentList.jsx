import React, { useState, useMemo } from "react";
import { useNavigate } from "react-router-dom";
import { useFilters } from "../../hooks/use-filters";
import { useScope } from "../../hooks/use-scope";
import { mockStudents, getCollegeById, getProgramById, getYearById, getSemesterById } from "../../lib/mock-data";
import { Search, ChevronDown, ChevronUp, User, GraduationCap, DollarSign } from "lucide-react";
import "./StudentList.css";

const SortField = {
  NAME: "name",
  STUDENT_ID: "studentId",
  GPA: "gpa",
  ENROLLMENT_STATUS: "enrollmentStatus"
};

const SortDirection = {
  ASC: "asc",
  DESC: "desc"
};

export const StudentList = () => {
  const navigate = useNavigate();
  const { getActiveFilters, filters } = useFilters();
  const { getFacultyOptions, getProgramOptions } = useScope();
  
  const [searchTerm, setSearchTerm] = useState("");
  const [sortField, setSortField] = useState(SortField.NAME);
  const [sortDirection, setSortDirection] = useState(SortDirection.ASC);
  const [currentPage, setCurrentPage] = useState(1);
  const studentsPerPage = 10;

  const activeFilters = getActiveFilters();
  const facultyOptions = getFacultyOptions();
  const programOptions = getProgramOptions();

  // Filter students based on active filters and search
  const filteredStudents = useMemo(() => {
    let result = [...mockStudents];

    // Filter by college/faculty
    if (activeFilters.collegeId) {
      result = result.filter(s => s.collegeId === activeFilters.collegeId);
    }

    // Filter by program
    if (activeFilters.programId) {
      result = result.filter(s => s.programId === activeFilters.programId);
    }

    // Filter by academic year
    if (activeFilters.academicYearId) {
      result = result.filter(s => s.academicYearId === activeFilters.academicYearId);
    }

    // Filter by semester
    if (activeFilters.semesterId) {
      result = result.filter(s => s.semesterId === activeFilters.semesterId);
    }

    // Filter by search term
    if (searchTerm) {
      const term = searchTerm.toLowerCase();
      result = result.filter(s => 
        s.firstName.toLowerCase().includes(term) ||
        s.lastName.toLowerCase().includes(term) ||
        s.studentId.toLowerCase().includes(term) ||
        s.email.toLowerCase().includes(term)
      );
    }

    // Sort
    result.sort((a, b) => {
      let comparison = 0;
      switch (sortField) {
        case "name":
          comparison = `${a.firstName} ${a.lastName}`.localeCompare(`${b.firstName} ${b.lastName}`);
          break;
        case "studentId":
          comparison = a.studentId.localeCompare(b.studentId);
          break;
        case "gpa":
          comparison = a.gpa - b.gpa;
          break;
        case "enrollmentStatus":
          comparison = a.enrollmentStatus.localeCompare(b.enrollmentStatus);
          break;
      }
      return sortDirection === "asc" ? comparison : -comparison;
    });

    return result;
  }, [activeFilters, searchTerm, sortField, sortDirection]);

  // Pagination
  const totalPages = Math.ceil(filteredStudents.length / studentsPerPage);
  const paginatedStudents = filteredStudents.slice(
    (currentPage - 1) * studentsPerPage,
    currentPage * studentsPerPage
  );

  const handleSort = (field) => {
    if (sortField === field) {
      setSortDirection(sortDirection === "asc" ? "desc" : "asc");
    } else {
      setSortField(field);
      setSortDirection("asc");
    }
  };

  const handleStudentClick = (student) => {
    navigate(`/students/detail/${student.id}`);
  };

  const getStatusBadgeClass = (status) => {
    switch (status) {
      case "Active": return "status-active";
      case "Inactive": return "status-inactive";
      case "Graduated": return "status-graduated";
      case "Suspended": return "status-suspended";
      case "Graduation Pending": return "status-pending";
      default: return "";
    }
  };

  const getFinancialBadgeClass = (status) => {
    switch (status) {
      case "Paid": return "financial-paid";
      case "Pending": return "financial-pending";
      case "Partial": return "financial-partial";
      case "Overdue": return "financial-overdue";
      default: return "";
    }
  };

  const SortIcon = ({ field }) => {
    if (sortField !== field) return <ChevronDown size={14} className="sort-icon-inactive" />;
    return sortDirection === "asc" ? <ChevronUp size={14} /> : <ChevronDown size={14} />;
  };

  return (
    <div className="student-list-container">
      {/* Header */}
      <div className="page-header">
        <div className="header-content">
          <GraduationCap size={28} className="header-icon" />
          <div>
            <h1>Students</h1>
            <p className="header-subtitle">
              {filteredStudents.length} student{filteredStudents.length !== 1 ? "s" : ""} found
              {activeFilters.collegeId && ` in ${getCollegeById(activeFilters.collegeId)?.name}`}
              {activeFilters.programId && ` - ${getProgramById(activeFilters.programId)?.name}`}
            </p>
          </div>
        </div>
      </div>

      {/* Search and Filters Bar */}
      <div className="search-filter-bar">
        <div className="search-box">
          <Search size={18} className="search-icon" />
          <input
            type="text"
            placeholder="Search by name, ID, or email..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="search-input"
          />
        </div>
        
        <div className="active-filters">
          {activeFilters.collegeId && (
            <span className="filter-tag">
              {getCollegeById(activeFilters.collegeId)?.name}
            </span>
          )}
          {activeFilters.programId && (
            <span className="filter-tag">
              {getProgramById(activeFilters.programId)?.name}
            </span>
          )}
          {activeFilters.academicYearId && (
            <span className="filter-tag">
              {getYearById(activeFilters.academicYearId)?.name}
            </span>
          )}
          {activeFilters.semesterId && (
            <span className="filter-tag">
              {getSemesterById(activeFilters.semesterId)?.name}
            </span>
          )}
        </div>
      </div>

      {/* Students Table */}
      <div className="students-table-wrapper">
        <table className="students-table">
          <thead>
            <tr>
              <th onClick={() => handleSort("name")} className="sortable">
                Student <SortIcon field="name" />
              </th>
              <th onClick={() => handleSort("studentId")} className="sortable">
                ID <SortIcon field="studentId" />
              </th>
              <th>Program</th>
              <th>Year</th>
              <th onClick={() => handleSort("gpa")} className="sortable">
                GPA <SortIcon field="gpa" />
              </th>
              <th onClick={() => handleSort("enrollmentStatus")} className="sortable">
                Status <SortIcon field="enrollmentStatus" />
              </th>
              <th>Financial</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {paginatedStudents.map((student) => {
              const college = getCollegeById(student.collegeId);
              const program = getProgramById(student.programId);
              return (
                <tr key={student.id} onClick={() => handleStudentClick(student)} className="student-row">
                  <td>
                    <div className="student-info-cell">
                      <div className="student-avatar">
                        {student.firstName.charAt(0)}{student.lastName.charAt(0)}
                      </div>
                      <div className="student-details">
                        <span className="student-name">
                          {student.firstName} {student.lastName}
                        </span>
                        <span className="student-email">{student.email}</span>
                      </div>
                    </div>
                  </td>
                  <td className="student-id-cell">{student.studentId}</td>
                  <td>
                    <div className="program-cell">
                      <span className="program-name">{program?.name || "N/A"}</span>
                      <span className="college-name">{college?.name}</span>
                    </div>
                  </td>
                  <td>
                    <span className="year-badge">2026</span>
                  </td>
                  <td>
                    <div className="gpa-cell">
                      <span className={`gpa-value ${student.gpa >= 3.5 ? "gpa-high" : student.gpa >= 2.5 ? "gpa-medium" : "gpa-low"}`}>
                        {student.gpa.toFixed(2)}
                      </span>
                      <span className="credits">/{student.totalCredits} cr</span>
                    </div>
                  </td>
                  <td>
                    <span className={`status-badge ${getStatusBadgeClass(student.enrollmentStatus)}`}>
                      {student.enrollmentStatus}
                    </span>
                  </td>
                  <td>
                    <span className={`financial-badge ${getFinancialBadgeClass(student.financialStatus)}`}>
                      <DollarSign size={12} />
                      {student.financialStatus}
                    </span>
                  </td>
                  <td>
                    <button className="view-btn" onClick={(e) => {
                      e.stopPropagation();
                      navigate(`/students/detail/${student.id}`);
                    }}>
                      View
                    </button>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>

        {paginatedStudents.length === 0 && (
          <div className="no-results">
            <User size={48} />
            <h3>No students found</h3>
            <p>Try adjusting your filters or search term</p>
          </div>
        )}
      </div>

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="pagination">
          <button
            onClick={() => setCurrentPage(p => Math.max(1, p - 1))}
            disabled={currentPage === 1}
            className="pagination-btn"
          >
            Previous
          </button>
          <span className="pagination-info">
            Page {currentPage} of {totalPages}
          </span>
          <button
            onClick={() => setCurrentPage(p => Math.min(totalPages, p + 1))}
            disabled={currentPage === totalPages}
            className="pagination-btn"
          >
            Next
          </button>
        </div>
      )}
    </div>
  );
};