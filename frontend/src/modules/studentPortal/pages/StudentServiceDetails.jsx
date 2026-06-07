import React, { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { ArrowLeft, Clock, CreditCard, FileText, CheckCircle } from "lucide-react";
import { getServiceById } from "../../studentServices/services/studentServicesService";
import LoadingSpinner from "../../studentServices/components/LoadingSpinner";
import StatusBadge from "../components/StatusBadge";
import "../styles/ServiceDetails.css";

const ServiceDetails = () => {
  const { id } = useParams();
  const navigate = useNavigate();
  const { t } = useTranslation();
  const [service, setService] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const loadService = async () => {
      setLoading(true);
      try {
        const data = await getServiceById(id);
        setService(data);
      } catch (err) {
        console.error(err);
      } finally {
        setLoading(false);
      }
    };
    loadService();
  }, [id]);

  if (loading) return <LoadingSpinner />;
  if (!service) return <div className="sd-notfound">{t("service_not_found")}</div>;

  return (
    <div className="sd-container">
      <button className="sd-back-btn" onClick={() => navigate("/student/dashboard")}>
        <ArrowLeft size={16} /> {t("back_to_dashboard")}
      </button>
      <div className="sd-header">
        <div className="sd-icon">📄</div>
        <div className="sd-title">
          <h1>{service.name}</h1>
          <div className="sd-badges">
            <StatusBadge status={service.isActive ? "active" : "inactive"} type="service" />
            {service.isPaid ? <span className="sd-badge-paid">${service.price}</span> : <span className="sd-badge-free">{t("free")}</span>}
          </div>
        </div>
      </div>
      <div className="sd-content">
        <div className="sd-main">
          <div className="sd-card"><h3>{t("description")}</h3><p>{service.description}</p></div>
          <div className="sd-card"><h3>{t("instructions")}</h3><p>{service.instructions || t("no_instructions")}</p></div>
          <div className="sd-card"><h3>{t("requirements")}</h3><ul>{service.requirements?.map((r,i)=><li key={i}>{r}</li>)}</ul></div>
          <div className="sd-card"><h3>{t("workflow_preview")}</h3>
            <div className="sd-workflow">
              {service.workflow?.steps?.map((step, idx) => (
                <div key={idx} className="sd-workflow-step">
                  <span className="sd-step-num">{idx+1}</span>
                  <span>{step.title}</span>
                  {idx < service.workflow.steps.length-1 && <span className="sd-step-arrow">→</span>}
                </div>
              ))}
            </div>
          </div>
        </div>
        <div className="sd-sidebar">
          <div className="sd-info-card">
            <div><Clock size={18} /> {t("estimated_processing")}: {service.estimatedProcessingDays || 5} {t("days")}</div>
            <div><CreditCard size={18} /> {service.isPaid ? `$${service.price}` : t("free")}</div>
            <div><FileText size={18} /> {t("category")}: {service.category || t("general")}</div>
          </div>
          <button className="sd-apply-btn" onClick={() => navigate(`/student/services/${id}/apply`)} disabled={!service.isActive}>
            {service.isActive ? t("apply_for_service") : t("service_closed")}
          </button>
        </div>
      </div>
    </div>
  );
};

export default ServiceDetails;