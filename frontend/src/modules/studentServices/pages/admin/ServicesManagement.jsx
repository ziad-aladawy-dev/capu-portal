import { useState, useEffect, useMemo } from "react";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useQueryClient } from "@tanstack/react-query";
import { Edit3, Trash2, Power, Eye, Search, Briefcase } from "lucide-react";
import { getAllServices, deleteService, toggleServiceStatus } from "../../services/studentServicesService";
import { fmtAmount } from "../../../../core/services/treasuryService";
import { getLocalized } from "../../../../core/utils/getLocalized";
import { SERVICE_TYPE, SERVICE_TYPE_LABELS } from "../../../../core/constants/requestStatus";
import EmptyState from "../../../../core/components/EmptyState";
import StatusBadge from "../../../../core/components/StatusBadge";
import LoadingSpinner from "../../../../core/components/LoadingSpinner";
import PageHeader from "../../../../core/components/PageHeader";
import ConfirmDialog from "../../../../core/components/ConfirmDialog";
import { useToast } from "../../../../core/components/Toast";
import "../../styles/admin/ServicesManagement.css";

const PAGE_SIZE = 12;

const ServicesManagement = () => {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const { addToast } = useToast();
  const queryClient = useQueryClient();
  const [services, setServices] = useState([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [error, setError] = useState(null);
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [page, setPage] = useState(1);

  const loadServices = async () => {
    try {
      const data = await getAllServices();
      setServices(Array.isArray(data) ? data : []);
    } catch (err) { setError(err.message); }
    finally { setLoading(false); }
  };

  // eslint-disable-next-line react-hooks/set-state-in-effect -- async loader; setState only after await
  useEffect(() => { loadServices(); }, []);

  const handleConfirmDelete = async () => {
    try {
      await deleteService(deleteTarget.id);
      queryClient.invalidateQueries({ queryKey: ["staff-dashboard"] });
      queryClient.invalidateQueries({ queryKey: ["ss-services"] });
      await loadServices();
      addToast(t("service_deleted"), "success");
    } catch (err) { addToast(err.message, "error"); }
    setDeleteTarget(null);
  };

  const handleToggleStatus = async (id) => {
    try {
      await toggleServiceStatus(id);
      await loadServices();
      queryClient.invalidateQueries({ queryKey: ["ss-services"] });
      addToast(t("status_toggled"), "success");
    } catch (err) { addToast(err.message, "error"); }
  };

  const getServiceTypeLabel = (type) => {
    if (typeof type === "number") { type = SERVICE_TYPE_LABELS[type] || "General"; }
    const labels = { General: t("general"), Specialized: t("specialized"), Administrative: t("administrative") };
    return labels[type] || type;
  };

  const filteredServices = useMemo(() =>
    services.filter((s) => getLocalized(s.name, i18n.language)?.toLowerCase().includes(search.toLowerCase())),
    [services, search, i18n.language]
  );

  const totalPages = Math.max(1, Math.ceil(filteredServices.length / PAGE_SIZE));
  const paged = useMemo(() => {
    const start = (page - 1) * PAGE_SIZE;
    return filteredServices.slice(start, start + PAGE_SIZE);
  }, [filteredServices, page]);

  if (loading && services.length === 0) return <LoadingSpinner fullPage />;
  if (error) return <div className="ssm-error">{error}</div>;

  return (
    <div className="ssm-page">
      <PageHeader
        icon={Briefcase}
        kicker={t("system_services")}
        title={t("services_management")}
        subtitle={`${filteredServices.length} ${t("services_found")}`}
        actions={
          <button className="btn-primary" onClick={() => navigate("/admin/student-services/services/create")}>
            {t("create_service")}
          </button>
        }
      />
      <div className="ssm-filter-card">
        <div className="ssm-search">
          <Search size={16} className="ssm-search-icon" />
          <input
            type="text"
            placeholder={t("search_services")}
            value={search}
            onChange={(e) => { setSearch(e.target.value); setPage(1); }}
          />
        </div>
      </div>
      {paged.length === 0 ? (
        <div className="ssm-table-card"><EmptyState title={t("no_services")} /></div>
      ) : (
        <>
          <div className="ssm-table-card">
            <table className="ssm-table">
              <thead>
                <tr>
                  <th style={{ textAlign: "center", width: "50px" }}>#</th>
                  <th>{t("service_name")}</th>
                  <th>{t("type")}</th>
                  <th>{t("pricing")}</th>
                  <th style={{ textAlign: "center" }}>{t("status")}</th>
                  <th style={{ textAlign: "center" }}>{t("actions")}</th>
                </tr>
              </thead>
              <tbody>
                {paged.map((service, idx) => (
                  <tr key={service.id}>
                    <td style={{ textAlign: "center" }}>{(page - 1) * PAGE_SIZE + idx + 1}</td>
                    <td>
                      <div className="ssm-service-name">{getLocalized(service.name, i18n.language)}</div>
                      <div className="ssm-service-desc">{getLocalized(service.description, i18n.language)}</div>
                    </td>
                    <td>
                      <span className={`ssm-type-badge ${service.type === SERVICE_TYPE.General || service.type === "General" ? "ssm-type-general" : service.type === SERVICE_TYPE.Specialized || service.type === "Specialized" ? "ssm-type-specialized" : "ssm-type-admin"}`}>
                        {getServiceTypeLabel(service.type)}
                      </span>
                    </td>
                    <td>
                      <span className={`ssm-price-badge ${service.isPaid ? "paid" : "free"}`}>
                        {service.isPaid ? `${fmtAmount(service.price)} EGP` : t("free")}
                      </span>
                    </td>
                    <td style={{ textAlign: "center" }}>
                      <StatusBadge status={service.isActive ? "active" : "inactive"} label={service.isActive ? t("active") : t("inactive")} />
                    </td>
                    <td style={{ textAlign: "center" }}>
                      <div className="ssm-actions">
                        <button className="ssm-action-btn edit" onClick={() => navigate(`/admin/student-services/services/${service.id}/edit`)} title={t("edit")}><Edit3 size={14} /></button>
                        <button className="ssm-action-btn info" onClick={() => navigate(`/admin/student-services/services/${service.id}`)} title={t("details")}><Eye size={14} /></button>
                        <button className="ssm-action-btn toggle" onClick={() => handleToggleStatus(service.id)} title={t("toggle_status")}><Power size={14} /></button>
                        <button className="ssm-action-btn delete" onClick={() => setDeleteTarget({ id: service.id, name: getLocalized(service.name, i18n.language) })} title={t("delete")}><Trash2 size={14} /></button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {totalPages > 1 && (
            <div className="ssm-pagination">
              <button className="btn-icon" onClick={() => setPage((p) => Math.max(1, p - 1))} disabled={page === 1}>{"<"}</button>
              <span>{t("page")} {page} / {totalPages}</span>
              <button className="btn-icon" onClick={() => setPage((p) => Math.min(totalPages, p + 1))} disabled={page === totalPages}>{">"}</button>
            </div>
          )}
        </>
      )}
      <ConfirmDialog
        open={deleteTarget != null}
        onClose={() => setDeleteTarget(null)}
        onConfirm={handleConfirmDelete}
        title={t("delete")}
        message={t("confirm_delete_message", { name: deleteTarget?.name || "" })}
        confirmLabel={t("delete")}
        cancelLabel={t("cancel")}
        variant="danger"
      />
    </div>
  );
};

export default ServicesManagement;
