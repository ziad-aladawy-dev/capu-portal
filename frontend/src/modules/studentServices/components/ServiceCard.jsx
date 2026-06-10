import React from "react";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import * as Icons from "lucide-react";
import "../styles/components/ServiceCard.css";

const ServiceCard = ({ service }) => {
  const navigate = useNavigate();
  const { t, i18n } = useTranslation();

  const getServiceTypeLabel = (type) => {
    if (typeof type === "number") {
      const types = { 1: "General", 2: "Specialized", 3: "Administrative" };
      type = types[type] || "General";
    }
    const labels = {
      General: t("general"),
      Specialized: t("specialized"),
      Administrative: t("administrative"),
    };
    return labels[type] || type;
  };

  const getServiceIcon = (name) => {
    const serviceName = name?.toLowerCase() || "";
    if (serviceName.includes("certificate") || serviceName.includes("بيان") || serviceName.includes("تخرج") || serviceName.includes("شهادة")) {
      return Icons.FileText;
    }
    if (serviceName.includes("card") || serviceName.includes("كارنيه") || serviceName.includes("هوية")) {
      return Icons.IdCard;
    }
    if (serviceName.includes("military") || serviceName.includes("تجنيد") || serviceName.includes("عسكري")) {
      return Icons.Shield;
    }
    if (serviceName.includes("money") || serviceName.includes("دفع") || serviceName.includes("مصاريف") || serviceName.includes("رسوم")) {
      return Icons.CreditCard;
    }
    return Icons.GraduationCap;
  };

  const getLocalizedText = (text) => {
    if (!text) return "";
    try {
      const parsed = JSON.parse(text);
      return i18n.language === "ar" ? parsed.ar || parsed.en : parsed.en || parsed.ar;
    } catch {
      return text;
    }
  };

  const LucideIcon = getServiceIcon(service.name);

  return (
    <div className="service-card" onClick={() => navigate(`/student/services/${service.id}`)}>
      <div className="service-card-top-line"></div>
      <div className="service-card-icon-wrapper">
        <LucideIcon size={20} className="service-lucide-icon" />
      </div>
      <h3>{getLocalizedText(service.name)}</h3>
      <p>{getLocalizedText(service.description) || t("service_no_description")}</p>
      <div className="service-card-meta">
        <span className={`service-price ${service.isPaid ? "paid" : "free"}`}>
          {service.isPaid ? `$${service.price}` : t("free")}
        </span>
        <span className="service-category">{getServiceTypeLabel(service.type)}</span>
      </div>
    </div>
  );
};

export default ServiceCard;