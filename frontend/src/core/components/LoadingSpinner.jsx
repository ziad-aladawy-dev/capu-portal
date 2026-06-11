import { useTranslation } from "react-i18next";
import "./loadingSpinner.css";

function LoadingSpinner({ message, fullPage = false }) {
  const { t } = useTranslation();
  return (
    <div className={`loading-spinner ${fullPage ? "full-page" : ""}`}>
      <div className="loading-spinner-ring" />
      <p>{message || t("loading")}</p>
    </div>
  );
}

export default LoadingSpinner;
