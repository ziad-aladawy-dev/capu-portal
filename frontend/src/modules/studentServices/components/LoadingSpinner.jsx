import React from "react";
import { useTranslation } from "react-i18next";
import "../styles/components/LoadingSpinner.css";

const LoadingSpinner = ({ message, fullPage = false }) => {
  const { t } = useTranslation();
  return (
    <div className={`spinner-container ${fullPage ? "full-page" : ""}`}>
      <div className="spinner"></div>
      <p>{message || t("loading")}</p>
    </div>
  );
};

export default LoadingSpinner;