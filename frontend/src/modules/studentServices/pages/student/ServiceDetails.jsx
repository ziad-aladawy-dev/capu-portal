import React, { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { DollarSign, Layers } from "lucide-react";
import { getServiceById } from "../../services/studentServicesService";
import LoadingSpinner from "../../../../core/components/LoadingSpinner";
import StatusBadge from "../../../../core/components/RequestStatusBadge";
import "../../styles/student/StudentServiceDetailsPage.css";

const ServiceDetails = () => {
  const { id } = useParams();
  const navigate = useNavigate();
  const { t, i18n } = useTranslation();
  const [service, setService] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    const load = async () => {
      try {
        const data = await getServiceById(id);
        setService(data);
      } catch (err) {
        setError(err.message);
      } finally {
        setLoading(false);
      }
    };
    load();
  }, [id]);

  const getLocalized = (text) => {
    if (!text) return "";
    try {
      const parsed = JSON.parse(text);
      return i18n.language === "ar" ? parsed.ar || parsed.en : parsed.en || parsed.ar;
    } catch { return text; }
  };

  const getTypeLabel = (type) => {
    if (typeof type === "number") { const map = { 1: "General", 2: "Specialized", 3: "Administrative" }; type = map[type] || "General"; }
    const labels = { General: t("general"), Specialized: t("specialized"), Administrative: t("administrative") };
    return labels[type] || type;
  };

  if (loading) return <LoadingSpinner />;
  if (error) return <div className="error-state">{error}</div>;
  if (!service) return <div className="error-state">{t("service_not_found")}</div>;

  return (
    <div className="student-service-details-page">
      <div className="details-container">
        <div className="details-card">
          <h1>{getLocalized(service.name)}</h1>
          <div className="meta-row">
            <span className="badge-type">{getTypeLabel(service.type)}</span>
            <StatusBadge status={service.isActive ? "active" : "inactive"} />
          </div>
          <p className="description">{getLocalized(service.description) || t("no_description")}</p>
          <div className="info-grid">
            <div className="info-item">
              <div className="info-icon"><DollarSign size={18} /></div>
              <div><span className="info-label">{t("pricing")}</span><span className="info-value">{service.isPaid ? `$${service.price}` : t("free")}</span></div>
            </div>
          </div>
          {service.workflow?.steps?.length > 0 && (
            <div className="workflow-section">
              <h3><Layers size={18} /> {t("workflow_steps")}</h3>
              <div className="workflow-steps-list">
                {service.workflow.steps.map((step, idx) => (
                  <div key={idx} className="step-item">
                    <span className="step-number">{idx+1}</span>
                    <span className="step-title">{step.title}</span>
                    {step.isRequired && <span className="step-required">{t("required")}</span>}
                  </div>
                ))}
              </div>
            </div>
          )}
          <div className="apply-section">
            <button className="apply-btn" onClick={() => navigate(`/student/services/${id}/apply`)} disabled={!service.isActive}>
              {service.isActive ? t("apply_for_service") : t("service_closed")}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};

export default ServiceDetails;