import { useTranslation } from "react-i18next";

function ErrorMessage({ message }) {
  const { t } = useTranslation();
  const displayMessage = message || t("something_went_wrong");

  return (
    <div
      style={{
        padding: "14px 16px",
        borderRadius: 12,
        background: "rgba(220,38,38,0.08)",
        border: "1px solid rgba(220,38,38,0.18)",
        color: "#dc2626",
        fontWeight: 700,
      }}
    >
      {displayMessage}
    </div>
  );
}
export default ErrorMessage;
