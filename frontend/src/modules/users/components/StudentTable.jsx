import { useRef, useEffect } from 'react';
import { Eye, Edit3 } from 'lucide-react';
import '../styles/UserTable.css';

const StudentTable = ({
  students,
  loading,
  error,
  pagination,
  onPageChange,
  onViewDetails,
  onEdit,
  selectedIds,
  onSelectionChange,
}) => {
  const selectAllRef = useRef(null);

  const allVisible = students.length > 0 && students.every((s) => selectedIds.has(s.id));
  const someVisible = students.some((s) => selectedIds.has(s.id));

  useEffect(() => {
    if (selectAllRef.current) {
      selectAllRef.current.indeterminate = someVisible && !allVisible;
    }
  }, [allVisible, someVisible]);

  if (loading) return <div className="table-container loading-state">Loading students...</div>;
  if (error) return <div className="table-container error-state">Error: {error}</div>;
  if (!students || students.length === 0) return <div className="table-container"><div className="empty-state">No students found</div></div>;

  const handleSelectAll = () => {
    const allVisibleSelected = students.every((s) => selectedIds.has(s.id));
    if (allVisibleSelected) {
      const newSet = new Set(selectedIds);
      students.forEach((s) => newSet.delete(s.id));
      onSelectionChange(newSet);
    } else {
      const newSet = new Set(selectedIds);
      students.forEach((s) => newSet.add(s.id));
      onSelectionChange(newSet);
    }
  };

  const handleRowSelect = (id) => {
    const newSet = new Set(selectedIds);
    if (newSet.has(id)) {
      newSet.delete(id);
    } else {
      newSet.add(id);
    }
    onSelectionChange(newSet);
  };

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
            <th className="bulk-check-cell">
              <input
                ref={selectAllRef}
                type="checkbox"
                className="bulk-checkbox"
                checked={allVisible && students.length > 0}
                onChange={handleSelectAll}
              />
            </th>
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
            <tr key={student.id} className={selectedIds.has(student.id) ? 'selected-row' : ''}>
              <td className="bulk-check-cell">
                <input
                  type="checkbox"
                  className="bulk-checkbox"
                  checked={selectedIds.has(student.id)}
                  onChange={() => handleRowSelect(student.id)}
                />
              </td>
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
          <button className="pagination-btn" onClick={() => onPageChange(pagination.pageNumber + 1)} disabled={pagination.pageNumber === pagination.totalPages}>
            &gt;
          </button>
        </div>
      )}
    </div>
  );
};

export default StudentTable;
