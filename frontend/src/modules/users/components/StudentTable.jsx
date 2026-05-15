import React from 'react';
import { Eye, Edit3, Key, Shield, ToggleRight, ToggleLeft, Trash2 } from 'lucide-react';
import '../styles/userTable.css';

const StudentTable = ({ 
  students, 
  loading, 
  error, 
  pagination, 
  onPageChange,
  onAction,
  onResetPassword,
  onViewDetails,
  onEdit,
  onPermissions
}) => {
  if (loading) {
    return (
      <div className="table-container loading-state">
        <div className="loading-spinner"></div>
        <p>Loading students...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="table-container error-state">
        <p style={{ color: '#dc2626' }}>Error occurred: {error}</p>
        <button onClick={() => window.location.reload()}>Try Again</button>
      </div>
    );
  }

  if (!students || students.length === 0) {
    return (
      <div className="table-container">
        <div className="empty-state">
          <p>No students found matching your criteria</p>
        </div>
      </div>
    );
  }

  const getPageNumbers = () => {
    const delta = 2;
    const range = [];
    const rangeWithDots = [];
    let l;
    const totalPages = pagination?.totalPages || 1;
    const currentPage = pagination?.pageNumber || 1;

    for (let i = 1; i <= totalPages; i++) {
      if (i === 1 || i === totalPages || (i >= currentPage - delta && i <= currentPage + delta)) {
        range.push(i);
      }
    }

    range.forEach((i) => {
      if (l) {
        if (i - l === 2) {
          rangeWithDots.push(l + 1);
        } else if (i - l !== 1) {
          rangeWithDots.push('...');
        }
      }
      rangeWithDots.push(i);
      l = i;
    });

    return rangeWithDots;
  };

  const isPasswordExpired = (student) => {
    return student.isPasswordExpired || 
      (student.passwordExpiryDate && new Date(student.passwordExpiryDate) < new Date());
  };

  const handleToggleActive = (student) => {
    onAction(student.id, student.isActive ? 'deactivate' : 'activate', 
      student.isActive ? 'Deactivate Student' : 'Activate Student');
  };

  const handleDelete = (student) => {
    if (window.confirm(`Are you sure you want to delete student "${student.fullNameEn}"?`)) {
      onAction(student.id, 'soft-delete', 'Delete Student');
    }
  };

  return (
    <div className="table-container">
      <table className="users-table">
        <thead>
          <tr>
            <th>#</th>
            <th>Student Code</th>
            <th>National ID</th>
            <th>Name (English)</th>
            <th>Name (Arabic)</th>
            <th>Email</th>
            <th>Status</th>
            <th>Password</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {students.map((student) => (
            <tr key={student.id} className={student.isDeleted ? 'deleted-row' : ''}>
              <td>{student.displayId || student.id?.substring(0, 8)}</td>
              <td style={{ fontFamily: 'Space Mono, monospace', fontWeight: 600 }}>{student.studentCode}</td>
              <td style={{ fontFamily: 'Space Mono, monospace' }}>{student.nationalId}</td>
              <td style={{ fontWeight: 600 }}>{student.fullNameEn}</td>
              <td>{student.fullNameAr}</td>
              <td>{student.email}</td>
              <td>
                <span className={`status-badge ${student.isActive ? 'status-active' : 'status-inactive'}`}>
                  <span className="status-dot"></span>
                  {student.isActive ? 'Active' : 'Inactive'}
                </span>
                {student.isDeleted && (
                  <span className="status-badge status-deleted">
                    <span className="status-dot"></span>
                    Deleted
                  </span>
                )}
              </td>
              <td>
                <span className={`password-badge ${isPasswordExpired(student) ? 'password-expired' : 'password-valid'}`}>
                  {isPasswordExpired(student) ? 'Expired' : 'Valid'}
                </span>
              </td>
              <td>
                <div className="action-buttons">
                  <button
                    className="action-btn info-btn"
                    onClick={() => onViewDetails(student.id)}
                    title="View Details"
                  >
                    <Eye size={16} />
                  </button>
                  <button
                    className="action-btn edit-btn"
                    onClick={() => onEdit(student.id)}
                    title="Edit Student"
                  >
                    <Edit3 size={16} />
                  </button>
                  <button
                    className="action-btn permission-btn"
                    onClick={() => onPermissions(student.id)}
                    title="Manage Permissions"
                  >
                    <Shield size={16} />
                  </button>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {/* Pagination */}
      {pagination && pagination.totalPages > 1 && (
        <div className="pagination-container">
          <button
            className="pagination-btn"
            onClick={() => onPageChange(pagination.pageNumber - 1)}
            disabled={pagination.pageNumber === 1}
          >
            &lt;
          </button>
          
          {getPageNumbers().map((page, index) => (
            <button
              key={index}
              className={`pagination-btn ${page === pagination.pageNumber ? 'active' : ''} ${page === '...' ? 'dots' : ''}`}
              onClick={() => typeof page === 'number' && onPageChange(page)}
              disabled={page === '...'}
            >
              {page}
            </button>
          ))}
          
          <button
            className="pagination-btn"
            onClick={() => onPageChange(pagination.pageNumber + 1)}
            disabled={pagination.pageNumber === pagination.totalPages}
          >
            &gt;
          </button>
        </div>
      )}
    </div>
  );
};

export default StudentTable;