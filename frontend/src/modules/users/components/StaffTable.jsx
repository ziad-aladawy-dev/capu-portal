import React from 'react';
import { Eye, Edit3 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import '../styles/UserTable.css';

const StaffTable = ({ 
  staff, 
  loading, 
  error, 
  pagination, 
  onPageChange,
  onViewDetails,
  onEdit
}) => {
  const { t } = useTranslation();

  if (loading) return <div className="table-container loading-state">{t("loading")}...</div>;
  if (error) return <div className="table-container error-state">{t("error")}: {error}</div>;
  if (!staff || staff.length === 0) return <div className="table-container"><div className="empty-state">{t("no_data")}</div></div>;

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
            <th>{t("staff_code")}</th>
            <th>{t("national_id")}</th>
            <th>{t("full_name")}</th>
            <th>{t("email")}</th>
            <th>{t("status")}</th>
            <th>{t("password_status")}</th>
            <th>{t("actions")}</th>
          </tr>
        </thead>
        <tbody>
          {staff.map((member, idx) => (
            <tr key={member.id}>
              <td>{((pagination.pageNumber - 1) * pagination.pageSize) + idx + 1}</td>
              <td style={{ fontFamily: 'Space Mono, monospace' }}>{member.employeeCode}</td>
              <td style={{ fontFamily: 'Space Mono, monospace' }}>{member.nationalId}</td>
              <td>{member.localizedName || member.name}</td>
              <td>{member.email}</td>
              <td>
                <span className={`status-badge ${member.isActive ? 'status-active' : 'status-inactive'}`}>
                  <span className="status-dot"></span>
                  {member.isActive ? t('active') : t('inactive')}
                </span>
              </td>
              <td>
                <span className={`password-badge ${member.passwordStatus === 'Expired' ? 'password-expired' : 'password-valid'}`}>
                  {member.passwordStatus === 'Expired' ? t('expired') : t('valid')}
                </span>
              </td>
              <td>
                <div className="action-buttons">
                  <button className="action-btn info-btn" onClick={() => onViewDetails(member.id)} title={t("view_details")}>
                    <Eye size={16} />
                  </button>
                  <button className="action-btn edit-btn" onClick={() => onEdit(member.id)} title={t("edit")}>
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