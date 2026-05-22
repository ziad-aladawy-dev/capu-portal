import React from 'react';
import PropTypes from 'prop-types';
import { Eye, Edit3 } from 'lucide-react';
import '../styles/UserTable.css';

const StudentTable = ({ 
  students, 
  loading, 
  error, 
  pagination, 
  onPageChange,
  onViewDetails,
  onEdit
}) => {
  if (loading) return <div className="table-container loading-state">Loading students...</div>;
  if (error) return <div className="table-container error-state">Error: {error}</div>;
  if (!students || students.length === 0) return <div className="table-container"><div className="empty-state">No students found</div></div>;

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
        if (i - l === 2) rangeWithDots.push(l + 1);
        else if (i - l !== 1) rangeWithDots.push('...');
      }
      rangeWithDots.push(i);
      l = i;
    });
    return rangeWithDots;
  };

  return (
    <div className="table-container">
      <table className="users-table">
        <thead>
          <tr>
            <th>#</th>
            <th>Student Code</th>
            <th>National ID</th>
            <th>Name</th>
            <th>Email</th>
            <th>Status</th>
            <th>Password</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {students.map((student, idx) => (
            <tr key={student.id}>
              <td>{((pagination.pageNumber - 1) * pagination.pageSize) + idx + 1}</td>
              <td style={{ fontFamily: 'Space Mono, monospace' }}>{student.studentCode}</td>
              <td style={{ fontFamily: 'Space Mono, monospace' }}>{student.nationalId}</td>
              <td>{student.name}</td>
              <td>{student.email}</td>
              <td>
                <span className={`status-badge ${student.isActive ? 'status-active' : 'status-inactive'}`}>
                  <span className="status-dot"></span>
                  {student.isActive ? 'Active' : 'Inactive'}
                </span>
              </td>
              <td>
                <span className={`password-badge ${student.passwordStatus === 'Expired' ? 'password-expired' : 'password-valid'}`}>
                  {student.passwordStatus || 'Valid'}
                </span>
              </td>
              <td>
                <div className="action-buttons">
                  <button className="action-btn info-btn" onClick={() => onViewDetails(student.id)} title="View Details">
                    <Eye size={16} />
                  </button>
                  <button className="action-btn edit-btn" onClick={() => onEdit(student.id)} title="Edit">
                    <Edit3 size={16} />
                  </button>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      {pagination && pagination.totalPages > 1 && (
        <div className="pagination-container">
          <button className="pagination-btn" onClick={() => onPageChange(pagination.pageNumber - 1)} disabled={pagination.pageNumber === 1}>
            &lt;
          </button>
          {getPageNumbers().map((page) => (
            <button
              key={page === '...' ? `dots-${pagination.pageNumber}` : page}
              className={`pagination-btn ${page === pagination.pageNumber ? 'active' : ''} ${page === '...' ? 'dots' : ''}`}
              onClick={() => typeof page === 'number' && onPageChange(page)}
              disabled={page === '...'}
            >
              {page}
            </button>
          ))}
          <button className="pagination-btn" onClick={() => onPageChange(pagination.pageNumber + 1)} disabled={pagination.pageNumber === pagination.totalPages}>
            &gt;
          </button>
        </div>
      )}
    </div>
  );
};

StudentTable.propTypes = {
  students: PropTypes.arrayOf(PropTypes.object).isRequired,
  loading: PropTypes.bool,
  error: PropTypes.string,
  pagination: PropTypes.shape({
    pageNumber: PropTypes.number.isRequired,
    pageSize: PropTypes.number.isRequired,
    totalPages: PropTypes.number.isRequired,
  }),
  onPageChange: PropTypes.func.isRequired,
  onViewDetails: PropTypes.func.isRequired,
  onEdit: PropTypes.func.isRequired,
};

export default StudentTable;