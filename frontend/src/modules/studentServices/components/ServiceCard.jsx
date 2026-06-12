import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import * as Icons from "lucide-react";
import { getLocalized } from "../../../core/utils/getLocalized";
import { SERVICE_TYPE_LABELS } from "../../../core/constants/requestStatus";
import { fmtAmount } from "../../../core/services/treasuryService";
import "../styles/components/ServiceCard.css";

// Module-scope static elements: picking a pre-built element avoids creating a
// component identity during render (react-hooks/static-components).
const SERVICE_ICONS = {
  certificate: <Icons.FileText size={20} className="service-lucide-icon" />,
  card: <Icons.IdCard size={20} className="service-lucide-icon" />,
  military: <Icons.Shield size={20} className="service-lucide-icon" />,
  money: <Icons.CreditCard size={20} className="service-lucide-icon" />,
  default: <Icons.GraduationCap size={20} className="service-lucide-icon" />,
};

const getServiceIconKey = (name) => {
  const serviceName = name?.toLowerCase() || "";
  if (serviceName.includes("certificate") || serviceName.includes("بيان") || serviceName.includes("تخرج") || serviceName.includes("شهادة")) return "certificate";
  if (serviceName.includes("card") || serviceName.includes("كارنيه") || serviceName.includes("هوية")) return "card";
  if (serviceName.includes("military") || serviceName.includes("تجنيد") || serviceName.includes("عسكري")) return "military";
  if (serviceName.includes("money") || serviceName.includes("دفع") || serviceName.includes("مصاريف") || serviceName.includes("رسوم")) return "money";
  return "default";
};

const ServiceCard = ({ service }) => {
  const navigate = useNavigate();
  const { t, i18n } = useTranslation();

  const getServiceTypeLabel = (type) => {
    if (typeof type === "number") {
      type = SERVICE_TYPE_LABELS[type] || "General";
    }
    const labels = {
      General: t("general"),
      Specialized: t("specialized"),
      Administrative: t("administrative"),
    };
    return labels[type] || type;
  };

  return (
    <div className="service-card" onClick={() => navigate(`/student/services/${service.id}`)}>
      <div className="service-card-top-line"></div>
      <div className="service-card-icon-wrapper">
        {SERVICE_ICONS[getServiceIconKey(service.name)]}
      </div>
      <h3>{getLocalized(service.name, i18n.language)}</h3>
      <p>{getLocalized(service.description, i18n.language) || t("service_no_description")}</p>
      <div className="service-card-meta">
        <span className={`service-price ${service.isPaid ? "paid" : "free"}`}>
          {service.isPaid ? `${fmtAmount(service.price)} EGP` : t("free")}
        </span>
        <span className="service-category">{getServiceTypeLabel(service.type)}</span>
      </div>
    </div>
  );
};

export default ServiceCard;
