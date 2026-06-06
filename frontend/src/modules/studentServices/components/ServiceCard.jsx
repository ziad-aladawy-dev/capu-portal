import React from "react";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import "../styles/components/ServiceCard.css";

const ServiceCard = ({ service }) => {
  const navigate = useNavigate();
  const { t } = useTranslation();

  return (
    <div className="service-card" onClick={() => navigate(`/student/services/${service.id}`)}>
      <div className="service-card-icon">📄</div>
      <h3>{service.name}</h3>
      <p>{service.description}</p>
      <div className="service-card-meta">
        <span className={`service-price ${service.isPaid ? "paid" : "free"}`}>
          {service.isPaid ? `$${service.price}` : t("free")}
        </span>
        <span className="service-category">{service.category}</span>
      </div>
    </div>
  );
};

export default ServiceCard;