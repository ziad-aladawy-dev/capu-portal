import { useRef, useEffect } from 'react';
import { Eye, Edit3 } from 'lucide-react';
import '../styles/UserTable.css';

const StaffTable = ({
  staff,
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

  const allVisible = staff.length > 0 && staff.every((s) => selectedIds.has(s.id));
  const someVisible = staff.some((s) => selectedIds.has(s.id));

  useEffect(() => {
    if (selectAllRef.current) {
      selectAllRef.current.indeterminate = someVisible && !allVisible;
    }
  }, [allVisible, someVisible]);

  if (loading) return <div className="table-container loading-state">Loading staff...</div>;
  if (error) return <div className="table-container error-state">Error: {error}</div>;
  if (!staff || staff.length === 0) return <div className="table-container"><div className="empty-state">No staff found</div></div>;

  const handleSelectAll = () => {
    const allVisibleSelected = staff.every((s) => selectedIds.has(s.id));
    if (allVisibleSelected) {
      const newSet = new Set(selectedIds);
      staff.forEach((s) => newSet.delete(s.id));
      onSelectionChange(newSet);
    } else {
      const newSet = new Set(selectedIds);
      staff.forEach((s) => newSet.add(s.id));
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
                checked={allVisible && staff.length > 0}
                onChange={handleSelectAll}
              />
            </th>
            <th>#</th>
            <th>Staff Code</th>
            <th>National ID</th>
            <th>Name</th>
            <th>Email</th>
            <th>Status</th>
            <th>Password</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {staff.map((member, idx) => (
            <tr key={member.id} className={selectedIds.has(member.id) ? 'selected-row' : ''}>
              <td className="bulk-check-cell">
                <input
                  type="checkbox"
                  className="bulk-checkbox"
                  checked={selectedIds.has(member.id)}
                  onChange={() => handleRowSelect(member.id)}
                />
              </td>
              <td>{((pagination.pageNumber - 1) * pagination.pageSize) + idx + 1}</td>
              <td style={{ fontFamily: 'Space Mono, monospace' }}>{member.employeeCode}</td>
              <td style={{ fontFamily: 'Space Mono, monospace' }}>{member.nationalId}</td>
              <td>{member.name}</td>
              <td>{member.email}</td>
              <td>
                <span className={`status-badge ${member.isActive ? 'status-active' : 'status-inactive'}`}>
                  <span className="status-dot"></span>
                  {member.isActive ? 'Active' : 'Inactive'}
                </span>
              </td>
              <td>
                <span className={`password-badge ${member.passwordStatus === 'Expired' ? 'password-expired' : 'password-valid'}`}>
                  {member.passwordStatus || 'Valid'}
                </span>
              </td>
              <td>
                <div className="action-buttons">
                  <button className="action-btn info-btn" onClick={() => onViewDetails(member.id)} title="View Details">
                    <Eye size={16} />
                  </button>
                  <button className="action-btn edit-btn" onClick={() => onEdit(member.id)} title="Edit">
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

export default StaffTable;
