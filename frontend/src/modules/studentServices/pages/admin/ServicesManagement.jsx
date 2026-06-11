import React, { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { Edit3, Trash2, Power, Eye, Search, Briefcase } from "lucide-react";
import { getAllServices, deleteService, toggleServiceStatus } from "../../services/studentServicesService";
import { getLocalized } from "../../../../core/utils/getLocalized";
import EmptyState from "../../../../core/components/EmptyState";
import StatusBadge from "../../../../core/components/StatusBadge";
import LoadingSpinner from "../../../../core/components/LoadingSpinner";
import PageHeader from "../../../../core/components/PageHeader";
import ConfirmDialog from "../../../../core/components/ConfirmDialog";
import { useToast } from "../../../../core/components/Toast";
import "../../styles/admin/ServicesManagement.css";

const ServicesManagement = () => {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const { addToast } = useToast();
  const [services, setServices] = useState([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [error, setError] = useState(null);
  const [deleteId, setDeleteId] = useState(null);

  useEffect(() => { loadServices(); }, []);

  const loadServices = async () => {
    setLoading(true);
    try {
      const data = await getAllServices();
      setServices(Array.isArray(data) ? data : []);
    } catch (err) { setError(err.message); }
    finally { setLoading(false); }
  };

  const handleConfirmDelete = async () => {
    try { await deleteService(deleteId); await loadServices(); } catch (err) { addToast(err.message, "error"); }
    setDeleteId(null);
  };

  const handleToggleStatus = async (id) => {
    try { await toggleServiceStatus(id); await loadServices(); } catch (err) { addToast(err.message, "error"); }
  };

  const getServiceTypeLabel = (type) => {
    if (typeof type === "number") { const types = { 1: "General", 2: "Specialized", 3: "Administrative" }; type = types[type] || "General"; }
    const labels = { General: t("general"), Specialized: t("specialized"), Administrative: t("administrative") };
    return labels[type] || type;
  };

  const filteredServices = services.filter(s => getLocalized(s.name, i18n.language)?.toLowerCase().includes(search.toLowerCase()));

  if (loading && services.length === 0) return <LoadingSpinner fullPage />;
  if (error) return <div className="ssm-error">{error}</div>;

  return (
    <div className="ssm-page">
      <PageHeader
        icon={Briefcase}
        kicker={t("system_services")}
        title={t("services_management")}
        actions={<button className="btn-primary" onClick={() => navigate("/admin/student-services/services/create")}>{t("create_service")}</button>}
      />
      <div className="ssm-filter-card"><div className="ssm-search"><Search size={16} className="ssm-search-icon" /><input type="text" placeholder={t("search_services")} value={search} onChange={e => setSearch(e.target.value)} /></div></div>
      {filteredServices.length === 0 ? <div className="ssm-table-card"><EmptyState title={t("no_services")} /></div> : (
        <div className="ssm-table-card"><table className="ssm-table"><thead><tr><th style={{textAlign:"center", width:"60px"}}>#</th><th>{t("service_name")}</th><th>{t("type")}</th><th>{t("pricing")}</th><th style={{textAlign:"center"}}>{t("status")}</th><th style={{textAlign:"center"}}>{t("actions")}</th></tr></thead><tbody>
          {filteredServices.map((service, idx) => (
            <tr key={service.id}><td style={{textAlign:"center"}}>{idx+1}</td><td><div style={{fontWeight:"700",fontSize:"14px"}}>{getLocalized(service.name, i18n.language)}</div><div style={{fontSize:"11px",color:"var(--color-text-secondary)",marginTop:"2px"}}>{getLocalized(service.description, i18n.language)}</div></td><td><span className={`ssm-type-badge ${service.type===1||service.type==='General'?'ssm-type-general':service.type===2||service.type==='Specialized'?'ssm-type-specialized':'ssm-type-admin'}`}>{getServiceTypeLabel(service.type)}</span></td><td><span className={`ssm-price-badge ${service.isPaid?'paid':'free'}`}>{service.isPaid?`$${service.price}`:t("free")}</span></td><td style={{textAlign:"center"}}><StatusBadge status={service.isActive?"active":"inactive"} label={service.isActive?t("active"):t("inactive")} /></td><td style={{textAlign:"center"}}><div className="ssm-actions"><button className="ssm-action-btn edit" onClick={() => navigate(`/admin/student-services/services/edit/${service.id}`)} title={t("edit")}><Edit3 size={14} /></button><button className="ssm-action-btn info" onClick={() => navigate(`/admin/student-services/services/${service.id}`)} title={t("details")}><Eye size={14} /></button><button className="ssm-action-btn toggle" onClick={() => handleToggleStatus(service.id)} title={t("toggle_status")}><Power size={14} /></button><button className="ssm-action-btn delete" onClick={() => setDeleteId(service.id)} title={t("delete")}><Trash2 size={14} /></button></div></td></tr>
          ))}
        </tbody></table></div>
      )}
      <ConfirmDialog
        open={deleteId != null}
        onClose={() => setDeleteId(null)}
        onConfirm={handleConfirmDelete}
        title={t("delete")}
        message={t("confirm_delete_message", { name: "" })}
        confirmLabel={t("delete")}
        cancelLabel={t("cancel")}
        variant="danger"
      />
    </div>
  );
};

export default ServicesManagement;