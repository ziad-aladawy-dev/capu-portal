import { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { Calendar, MapPin, Layers, ToggleRight, FileText, Briefcase, ArrowLeft, Edit3, Banknote } from "lucide-react";
import { getServiceById } from "../../services/studentServicesService";
import { getLocalized } from "../../../../core/utils/getLocalized";
import { fmtAmount } from "../../../../core/services/treasuryService";
import { SERVICE_TYPE_LABELS } from "../../../../core/constants/requestStatus";
import { usePermission } from "../../../../core/auth/usePermission";
import LoadingSpinner from "../../../../core/components/LoadingSpinner";
import ErrorMessage from "../../../../core/components/ErrorMessage";
import PageHeader from "../../../../core/components/PageHeader";
import StatusBadge from "../../../../core/components/StatusBadge";
import "../../styles/admin/ServiceDetailsPage.css";

const ServiceDetailsPage = () => {
  const { id } = useParams();
  const navigate = useNavigate();
  const { t, i18n } = useTranslation();
  const { can } = usePermission();
  const [service, setService] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [activeTab, setActiveTab] = useState("info");

  const loadService = async () => {
    try { const data = await getServiceById(id); setService(data); }
    catch (err) { setError(err.message); }
    finally { setLoading(false); }
  };

  // eslint-disable-next-line react-hooks/set-state-in-effect -- async loader; setState only after await
  useEffect(() => { loadService(); }, [id]); // eslint-disable-line react-hooks/exhaustive-deps

  const getServiceTypeLabel = (type) => {
    if (typeof type === "number") { type = SERVICE_TYPE_LABELS[type] || "General"; }
    const labels = { General: t("general"), Specialized: t("specialized"), Administrative: t("administrative") };
    return labels[type] || type;
  };

  if (loading) return <LoadingSpinner />;
  if (error) return <ErrorMessage message={error} />;
  if (!service) return <ErrorMessage message={t("service_not_found")} />;

  const hasWorkflow = service.workflow?.steps?.length > 0;
  const localizedName = getLocalized(service.name, i18n.language);
  const localizedDescription = getLocalized(service.description, i18n.language);
  const priceLabel = service.isPaid ? `${fmtAmount(service.price)} EGP` : t("free");
  const canEdit = can("student-services.services.editclose");

  return (
    <div className="sdp">
      <PageHeader
        icon={Briefcase}
        kicker={t("system_services")}
        title={localizedName}
        subtitle={getServiceTypeLabel(service.type)}
        leading={
          <button className="btn-icon" onClick={() => navigate("/admin/student-services/services")} title={t("back_to_dashboard")} aria-label={t("back_to_dashboard")}>
            <ArrowLeft size={16} />
          </button>
        }
        actions={canEdit && (
          <button className="btn-primary" onClick={() => navigate(`/admin/student-services/services/${service.id}/edit`)}>
            <Edit3 size={14} /> {t("edit_service")}
          </button>
        )}
      />
      <div className="sdp-body">
        <div className="sdp-meta-row">
          <StatusBadge status={service.isActive ? "active" : "inactive"} label={service.isActive ? t("active") : t("inactive")} />
          <span className={`sdp-hbadge ${service.isPaid ? "sdp-hb-paid" : "sdp-hb-free"}`}>{priceLabel}</span>
          {(service.academicYearName || service.academicYearId) && (
            <span className="sdp-stat-pill"><span>{t("academic_year")}</span><strong>{service.academicYearName || service.academicYearId}</strong></span>
          )}
          {(service.semesterName || service.semesterId) && (
            <span className="sdp-stat-pill"><span>{t("semester")}</span><strong>{service.semesterName || service.semesterId}</strong></span>
          )}
          {hasWorkflow && (
            <span className="sdp-stat-pill"><span>{t("workflow_steps")}</span><strong>{t("steps_count", { count: service.workflow.steps.length })}</strong></span>
          )}
        </div>
        <div className="sdp-tabs-row">
          <button className={`sdp-tab${activeTab === "info" ? " sdp-tab-active" : ""}`} onClick={() => setActiveTab("info")}>{t("info")}</button>
          <button className={`sdp-tab${activeTab === "scope" ? " sdp-tab-active" : ""}`} onClick={() => setActiveTab("scope")}>{t("structural_scope")}</button>
          <button className={`sdp-tab${activeTab === "workflow" ? " sdp-tab-active" : ""}`} onClick={() => setActiveTab("workflow")}>{t("workflow_steps")}</button>
        </div>
        {activeTab === "info" && (
          <div className="sdp-card sdp-fade">
            <div className="sdp-sec-title">{t("service_details")}</div>
            <div className="sdp-info-grid">
              <div className="sdp-info-item sdp-full"><div className="sdp-info-icon"><FileText size={14} /></div><div><span className="sdp-info-label">{t("description")}</span><span className="sdp-info-value sdp-muted">{localizedDescription || t("no_description")}</span></div></div>
              <div className="sdp-info-item"><div className="sdp-info-icon"><Banknote size={14} /></div><div><span className="sdp-info-label">{t("pricing")}</span><span className="sdp-info-value">{priceLabel}</span></div></div>
              <div className="sdp-info-item"><div className="sdp-info-icon"><Layers size={14} /></div><div><span className="sdp-info-label">{t("type")}</span><span className="sdp-info-value">{getServiceTypeLabel(service.type)}</span></div></div>
              {service.academicYearId && (<div className="sdp-info-item"><div className="sdp-info-icon"><Calendar size={14} /></div><div><span className="sdp-info-label">{t("academic_year")}</span><span className="sdp-info-value">{service.academicYearName || service.academicYearId}</span></div></div>)}
              {service.semesterId && (<div className="sdp-info-item"><div className="sdp-info-icon"><Calendar size={14} /></div><div><span className="sdp-info-label">{t("semester")}</span><span className="sdp-info-value">{service.semesterName || service.semesterId}</span></div></div>)}
              <div className="sdp-info-item"><div className="sdp-info-icon"><ToggleRight size={14} /></div><div><span className="sdp-info-label">{t("status")}</span><span className="sdp-info-value">{service.isActive ? t("active") : t("inactive")}</span></div></div>
            </div>
          </div>
        )}
        {activeTab === "scope" && (
          <div className="sdp-card sdp-fade">
            <div className="sdp-sec-title">{t("structural_scope")}</div>
            <div className="sdp-info-grid">
              <div className="sdp-info-item sdp-full"><div className="sdp-info-icon"><MapPin size={14} /></div><div><span className="sdp-info-label">{t("assigned_nodes")}</span><div className="sdp-scope-list">{service.scopeNodesDetails?.length > 0 ? service.scopeNodesDetails.map(node => (<span key={node.id} className="sdp-scope-tag">{node.localizedName}</span>)) : <span className="sdp-info-value">{t("global_scope")}</span>}</div>{service.includeDescendants && (<span className="sdp-hint">{t("include_descendants")}</span>)}</div></div>
            </div>
          </div>
        )}
        {activeTab === "workflow" && (
          <div className="sdp-card sdp-fade">
            <div className="sdp-sec-title">{t("workflow_steps")}</div>
            {hasWorkflow ? (
              <div className="sdp-step-list">
                {service.workflow.steps.map((step, idx) => (
                  <div key={idx} className="sdp-step"><div className="sdp-step-num">{idx + 1}</div><span className="sdp-step-title">{step.title}</span>{step.isRequired && <span className="sdp-step-req">{t("required")}</span>}</div>
                ))}
              </div>
            ) : <p className="sdp-empty">{t("no_workflow_steps")}</p>}
          </div>
        )}
      </div>
    </div>
  );
};

export default ServiceDetailsPage;
