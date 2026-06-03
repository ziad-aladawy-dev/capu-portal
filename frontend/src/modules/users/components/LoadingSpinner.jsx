import { useTranslation } from "react-i18next";

function LoadingSpinner({ message, fullPage = false }) {
  const { t } = useTranslation();
  const displayMessage = message || t("loading");

  return (
    <div
      style={{
        minHeight: fullPage ? "100vh" : 160,
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        gap: 12,
        color: "#1a1f5e",
        fontWeight: 700,
      }}
    >
      <div
        style={{
          width: 34,
          height: 34,
          borderRadius: "50%",
          border: "4px solid #e5e7eb",
          borderTopColor: "#c9a84c",
          animation: "spin 0.8s linear infinite",
        }}
      />
      <style>{`@keyframes spin { to { transform: rotate(360deg); } }`}</style>
      <span>{displayMessage}</span>
    </div>
  );
}
export default LoadingSpinner;