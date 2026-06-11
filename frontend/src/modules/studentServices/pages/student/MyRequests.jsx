import React, { useEffect, useState, useMemo } from "react";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { Search, ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight } from "lucide-react";
import { useStudentRequests } from "../../hooks/useStudentRequests";
import LoadingSpinner from "../../../../core/components/LoadingSpinner";
import EmptyState from "../../../../core/components/EmptyState";
import StatusBadge from "../../../../core/components/RequestStatusBadge";
import "../../styles/student/MyRequests.css";

const MyRequests = () => {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { requests, loading, loadRequests } = useStudentRequests();

  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [searchTerm, setSearchTerm] = useState("");
  const [filterStatus, setFilterStatus] = useState("");

  useEffect(() => { loadRequests(); }, [loadRequests]);

  let filtered = useMemo(() => {
    let result = requests || [];
    if (filterStatus) result = result.filter(req => req.status === filterStatus);
    return result;
  }, [requests, filterStatus]);

  const searched = useMemo(() => {
    if (!searchTerm) return filtered;
    const term = searchTerm.trim().toLowerCase();
    return filtered.filter(req => req.requestNumber?.toString().includes(term));
  }, [filtered, searchTerm]);

  const sorted = useMemo(() => [...searched].sort((a,b) => (b.requestNumber||0)-(a.requestNumber||0)), [searched]);

  const totalItems = sorted.length;
  const totalPages = Math.ceil(totalItems / pageSize);
  const startIndex = (currentPage-1)*pageSize;
  const paginatedItems = sorted.slice(startIndex, startIndex+pageSize);

  const handlePageChange = (page) => { if(page>=1 && page<=totalPages) setCurrentPage(page); };
  const handlePageSizeChange = (e) => { setPageSize(parseInt(e.target.value)); setCurrentPage(1); };
  const handleSearchChange = (e) => { setSearchTerm(e.target.value); setCurrentPage(1); };
  const handleFilterChange = (e) => { setFilterStatus(e.target.value); setCurrentPage(1); };

  const firstItem = totalItems===0 ? 0 : startIndex+1;
  const lastItem = Math.min(startIndex+pageSize, totalItems);

  if (loading && !requests.length) return <LoadingSpinner />;

  return (
    <div className="mr-container">
      <div className="mr-header">
        <div className="mr-header-icon"><svg width="20" height="20" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg"><path d="M20 7H4C2.9 7 2 7.9 2 9V19C2 20.1 2.9 21 4 21H20C21.1 21 22 20.1 22 19V9C22 7.9 21.1 7 20 7Z" stroke="currentColor" strokeWidth="1.5" fill="none"/><path d="M16 21V5C16 3.9 15.1 3 14 3H10C8.9 3 8 3.9 8 5V21" stroke="currentColor" strokeWidth="1.5" fill="none"/></svg></div>
        <div><span className="mr-header-kicker">{t("student_portal")}</span><h1>{t("my_requests")}</h1><p>{t("manage_your_requests")}</p></div>
      </div>
      <div className="mr-filters-bar">
        <div className="mr-search-box"><Search size={16} /><input type="text" placeholder={t("search_by_request_number")} value={searchTerm} onChange={handleSearchChange} /></div>
        <div className="mr-status-filter"><select value={filterStatus} onChange={handleFilterChange}><option value="">{t("all_statuses")}</option><option value="Draft">{t("draft")}</option><option value="Pending">{t("pending")}</option><option value="UnderReview">{t("under_review")}</option><option value="Approved">{t("approved")}</option><option value="Rejected">{t("rejected")}</option><option value="Completed">{t("completed")}</option></select></div>
      </div>
      {paginatedItems.length === 0 ? <EmptyState title={t("no_requests")} /> : (
        <>
          <div className="mr-table-container"><table className="mr-table"><thead><tr><th>{t("request_number")}</th><th>{t("service")}</th><th>{t("status")}</th><th>{t("submitted_date")}</th><th>{t("payment_status")}</th><th>{t("actions")}</th></tr></thead><tbody>
            {paginatedItems.map(req => (
              <tr key={req.id}><td className="mr-number">{req.requestNumber}</td><td>{req.serviceName}</td><td><StatusBadge status={req.status} /></td><td>{req.submittedAt ? new Date(req.submittedAt).toLocaleDateString() : "-"}</td><td><StatusBadge status={req.paymentStatus} type="payment" /></td><td><button className="mr-review-btn" onClick={() => navigate(`/student/requests/${req.id}`)}>{t("view_details")}</button></td></tr>
            ))}
          </tbody></table></div>
          <div className="mr-pagination-footer">
            <div className="mr-page-size-control"><label>{t("show")}</label><select value={pageSize} onChange={handlePageSizeChange}><option value="10">10</option><option value="20">20</option><option value="50">50</option><option value="100">100</option></select><span>{t("entries_per_page")}</span></div>
            <div className="mr-pagination-buttons">
              <button onClick={() => handlePageChange(1)} disabled={currentPage===1}><ChevronsLeft size={16} /></button>
              <button onClick={() => handlePageChange(currentPage-1)} disabled={currentPage===1}><ChevronLeft size={16} /></button>
              <span className="mr-page-info">{t("page")} {currentPage} / {totalPages}</span>
              <button onClick={() => handlePageChange(currentPage+1)} disabled={currentPage===totalPages}><ChevronRight size={16} /></button>
              <button onClick={() => handlePageChange(totalPages)} disabled={currentPage===totalPages}><ChevronsRight size={16} /></button>
            </div>
            <div className="mr-results-info">{t("showing_results", { first: firstItem, last: lastItem, total: totalItems })}</div>
          </div>
        </>
      )}
    </div>
  );
};

export default MyRequests;