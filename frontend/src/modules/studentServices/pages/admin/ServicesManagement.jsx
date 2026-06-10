import React, { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { Edit3, Trash2, Power, Eye, Search, Briefcase } from "lucide-react";
import { getAllServices, deleteService, toggleServiceStatus } from "../../services/studentServicesService";
import EmptyState from "../../components/EmptyState";
import "../../styles/admin/ServicesManagement.css";

const ServicesManagement = () => {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const [services, setServices] = useState([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [error, setError] = useState(null);

  useEffect(() => { loadServices(); }, []);

  const loadServices = async () => {
    setLoading(true);
    try {
      const data = await getAllServices();
      setServices(Array.isArray(data) ? data : []);
    } catch (err) { setError(err.message); }
    finally { setLoading(false); }
  };

  const handleDelete = async (id) => {
    if (window.confirm(t("confirm_delete_message", { name: "" }))) {
      try { await deleteService(id); await loadServices(); } catch (err) { alert(err.message); }
    }
  };

  const handleToggleStatus = async (id) => {
    try { await toggleServiceStatus(id); await loadServices(); } catch (err) { alert(err.message); }
  };

  const getServiceTypeLabel = (type) => {
    if (typeof type === "number") { const types = { 1: "General", 2: "Specialized", 3: "Administrative" }; type = types[type] || "General"; }
    const labels = { General: t("general"), Specialized: t("specialized"), Administrative: t("administrative") };
    return labels[type] || type;
  };

  const filteredServices = services.filter(s => s.name?.toLowerCase().includes(search.toLowerCase()));

  if (loading && services.length === 0) return <div className="table-container loading-state"><div className="loading-spinner"></div><span>{t("loading...")}</span></div>;
  if (error) return <div className="table-container error-state">{error}</div>;

  return (
    <div className="users-page">
      <div className="users-page-header">
        <div className="users-page-title"><div className="users-page-icon"><Briefcase size={20} /></div><div><span className="users-page-kicker">{t("system_services")}</span><h1>{t("services_management")}</h1></div></div>
        <div className="users-page-actions"><button className="users-primary-btn" onClick={() => navigate("/admin/student-services/services/create")}>{t("create_service")}</button></div>
      </div>
      <div className="users-filter-card"><div className="users-filter-row"><div className="users-search-box"><Search size={16} className="users-search-icon" /><input type="text" placeholder={t("search_services")} value={search} onChange={e => setSearch(e.target.value)} /></div></div></div>
      {filteredServices.length === 0 ? <div className="table-container empty-state"><EmptyState message={t("no_services")} /></div> : (
        <div className="table-container"><table className="users-table"><thead><tr><th style={{textAlign:"center", width:"60px"}}>#</th><th>{t("service_name")}</th><th>{t("type")}</th><th>{t("pricing")}</th><th style={{textAlign:"center"}}>{t("status")}</th><th style={{textAlign:"center"}}>{t("actions")}</th></tr></thead><tbody>
          {filteredServices.map((service, idx) => (
            <tr key={service.id}><td style={{textAlign:"center"}}>{idx+1}</td><td><div style={{fontWeight:"700",fontSize:"14px"}}>{service.name}</div><div style={{fontSize:"11px",color:"#6b7280",marginTop:"2px"}}>{service.description}</div></td><td><span className={`role-badge ${service.type===1||service.type==='General'?'role-student':service.type===2||service.type==='Specialized'?'role-professor':'role-admin'}`}>{getServiceTypeLabel(service.type)}</span></td><td><span className={`password-badge ${service.isPaid?'password-expired':'password-valid'}`}>{service.isPaid?`$${service.price}`:t("free")}</span></td><td style={{textAlign:"center"}}><span className={`status-badge ${service.isActive?"status-active":"status-inactive"}`}><span className="status-dot"></span>{service.isActive?t("active"):t("inactive")}</span></td><td style={{textAlign:"center"}}><div className="action-buttons" style={{justifyContent:"center"}}><button className="action-btn edit-btn" onClick={() => navigate(`/admin/student-services/services/edit/${service.id}`)} title={t("edit")}><Edit3 size={14} /></button><button className="action-btn info-btn" onClick={() => navigate(`/admin/student-services/services/${service.id}`)} title={t("details")}><Eye size={14} /></button><button className="action-btn toggle-btn" onClick={() => handleToggleStatus(service.id)} title={t("toggle_status")}><Power size={14} /></button><button className="action-btn delete-btn" onClick={() => handleDelete(service.id)} title={t("delete")}><Trash2 size={14} /></button></div></td></tr>
          ))}
        </tbody></table></div>
      )}
    </div>
  );
};

export default ServicesManagement;