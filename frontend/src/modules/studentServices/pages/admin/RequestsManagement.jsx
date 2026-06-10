import React, { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { Search, ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight } from "lucide-react";
import { useStaffRequestsPaged } from "../../hooks/useStaffRequestsPaged";
import LoadingSpinner from "../../components/LoadingSpinner";
import EmptyState from "../../components/EmptyState";
import StatusBadge from "../../components/StatusBadge";
import "../../styles/admin/RequestsManagement.css";

const getLocalizedStudentName = (nameJson, lang) => { if (!nameJson) return ""; try { const parsed = JSON.parse(nameJson); if (lang === "ar") return parsed.ar || parsed.en || ""; return parsed.en || parsed.ar || ""; } catch { return nameJson; } };

const RequestsManagement = () => {
  const { t, i18n } = useTranslation(); const navigate = useNavigate(); const [filterStatus, setFilterStatus] = useState("");
  const { requests, loading, error, pagination, search, sortBy, ascending, changePage, changePageSize, applySearch, applySort, refresh } = useStaffRequestsPaged(10);
  useEffect(() => { if (sortBy === null) applySort("requestnumber"); }, []);
  const filteredRequests = filterStatus ? requests.filter(req => req.status === filterStatus) : requests;
  const handleSort = (field) => { if (field === "requestnumber" || field === "studentname") applySort(field); };
  const getSortIcon = (field) => sortBy !== field ? null : ascending ? "↑" : "↓";
  const handlePageSizeChange = (e) => changePageSize(parseInt(e.target.value));
  const firstItem = pagination.totalCount===0 ? 0 : (pagination.page-1)*pagination.pageSize+1;
  const lastItem = Math.min(pagination.page*pagination.pageSize, pagination.totalCount);
  if (loading && requests.length===0) return <LoadingSpinner />;
  if (error) return <div className="error-message">{error}</div>;
  return (
    <div className="rm-container">
      <div className="rm-header"><div className="rm-header-icon"><svg width="20" height="20" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg"><path d="M20 7H4C2.9 7 2 7.9 2 9V19C2 20.1 2.9 21 4 21H20C21.1 21 22 20.1 22 19V9C22 7.9 21.1 7 20 7Z" stroke="currentColor" strokeWidth="1.5" fill="none"/><path d="M16 21V5C16 3.9 15.1 3 14 3H10C8.9 3 8 3.9 8 5V21" stroke="currentColor" strokeWidth="1.5" fill="none"/></svg></div><div><span className="rm-header-kicker">{t("student_services")}</span><h1>{t("student_requests")}</h1><p>{t("manage_student_requests")}</p></div></div>
      <div className="rm-filters-bar"><div className="rm-search-box"><Search size={16} /><input type="text" placeholder={t("search_by_request_number_or_student")} value={search} onChange={e => applySearch(e.target.value)} /></div><div className="rm-status-filter"><select value={filterStatus} onChange={e => setFilterStatus(e.target.value)}><option value="">{t("all_statuses")}</option><option value="Pending">{t("Pending")}</option><option value="UnderReview">{t("UnderReview")}</option><option value="Approved">{t("Approved")}</option><option value="Rejected">{t("Rejected")}</option><option value="Completed">{t("Completed")}</option><option value="Cancelled">{t("Cancelled")}</option></select></div></div>
      {filteredRequests.length===0 ? <EmptyState message={t("no_requests")} /> : (
        <>
          <div className="rm-table-container"><table className="rm-table"><thead><tr><th className="sortable" onClick={()=>handleSort("requestnumber")}>{t("request_number")} {getSortIcon("requestnumber")}</th><th className="sortable" onClick={()=>handleSort("studentname")}>{t("student_name")} {getSortIcon("studentname")}</th><th>{t("service_name")}</th><th>{t("status")}</th><th>{t("date")}</th><th>{t("payment_status")}</th><th>{t("actions")}</th></tr></thead><tbody>{filteredRequests.map(req => (<tr key={req.id}><td className="rm-number">{req.requestNumber}</td><td>{getLocalizedStudentName(req.studentName, i18n.language)}</td><td>{req.serviceName}</td><td><StatusBadge status={req.status} /></td><td>{req.submittedAt ? new Date(req.submittedAt).toLocaleDateString() : "-"}</td><td><StatusBadge status={req.paymentStatus} /></td><td><button className="rm-review-btn" onClick={() => navigate(`/admin/student-services/requests/${req.id}`)}>{t("review")}</button></td></tr>))}</tbody></table></div>
          <div className="rm-pagination-footer"><div className="rm-page-size-control"><label>{t("show")}</label><select value={pagination.pageSize} onChange={handlePageSizeChange}><option value="10">10</option><option value="20">20</option><option value="50">50</option><option value="100">100</option></select><span>{t("entries_per_page")}</span></div><div className="rm-pagination-buttons"><button onClick={()=>changePage(1)} disabled={pagination.page===1}><ChevronsLeft size={16} /></button><button onClick={()=>changePage(pagination.page-1)} disabled={pagination.page===1}><ChevronLeft size={16} /></button><span className="rm-page-info">{t("page")} {pagination.page} / {pagination.totalPages}</span><button onClick={()=>changePage(pagination.page+1)} disabled={pagination.page===pagination.totalPages}><ChevronRight size={16} /></button><button onClick={()=>changePage(pagination.totalPages)} disabled={pagination.page===pagination.totalPages}><ChevronsRight size={16} /></button></div><div className="rm-results-info">{t("showing_results", { first: firstItem, last: lastItem, total: pagination.totalCount })}</div></div>
        </>
      )}
    </div>
  );
};

export default RequestsManagement;