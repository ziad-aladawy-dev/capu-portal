import { useTranslation } from "react-i18next";
import {
  CheckCircle, Clock, AlertCircle, Send, UserCheck, XCircle, CreditCard, FileText,
} from "lucide-react";
import "../../studentServices/styles/components/RequestTimeline.css";

// Backend HistoryEntryDto: { action, comment, performedByUserId, performedByRole, performedAt }.
const RequestTimeline = ({ timeline = [] }) => {
  const { t } = useTranslation();

  const getIcon = (action) => {
    const lower = (action || "").toLowerCase();
    if (lower.includes("approve")) return <CheckCircle size={16} />;
    if (lower.includes("reject")) return <XCircle size={16} />;
    if (lower.includes("assign")) return <UserCheck size={16} />;
    if (lower.includes("comment")) return <Send size={16} />;
    if (lower.includes("payment")) return <CreditCard size={16} />;
    if (lower.includes("submit")) return <FileText size={16} />;
    if (lower.includes("moreinfo") || lower.includes("info")) return <AlertCircle size={16} />;
    return <Clock size={16} />;
  };

  // "StatusChanged_Approved" → "Status Changed · Approved"; "PaymentCompleted" → "Payment Completed".
  const prettyAction = (action) => {
    if (!action) return "";
    return action
      .replace(/_/g, " · ")
      .replace(/([a-z])([A-Z])/g, "$1 $2")
      .trim();
  };

  const formatDate = (dateStr) => (dateStr ? new Date(dateStr).toLocaleString() : "");

  if (!timeline || timeline.length === 0) {
    return (
      <div className="rt-timeline">
        <h3 className="rt-title">{t("timeline", { defaultValue: "Timeline" })}</h3>
        <div className="rt-empty">{t("no_timeline_events", { defaultValue: "No activity yet." })}</div>
      </div>
    );
  }

  return (
    <div className="rt-timeline">
      <h3 className="rt-title">{t("timeline", { defaultValue: "Timeline" })}</h3>
      <div className="rt-items">
        {timeline.map((item, idx) => (
          <div key={idx} className="rt-item">
            <div className="rt-icon">{getIcon(item.action)}</div>
            <div className="rt-content">
              <span className="rt-action">{prettyAction(item.action)}</span>
              <span className="rt-date">{formatDate(item.performedAt)}</span>
              {item.comment && <p className="rt-comment">{item.comment}</p>}
              {item.performedByRole && (
                <span className="rt-performer">
                  {t("by", { defaultValue: "by" })} {item.performedByRole}
                </span>
              )}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
};

export default RequestTimeline;
