
import { useTranslation } from "react-i18next";
import { CheckCircle, Clock, AlertCircle, Send, UserCheck, XCircle, FileText } from "lucide-react";
import "../styles/components/RequestTimeline.css";

const RequestTimeline = ({ timeline = [] }) => {
  const { t } = useTranslation();

  const getIcon = (action) => {
    const lower = action?.toLowerCase() || "";
    if (lower.includes("approve")) return <CheckCircle size={16} />;
    if (lower.includes("reject")) return <XCircle size={16} />;
    if (lower.includes("assign")) return <UserCheck size={16} />;
    if (lower.includes("comment")) return <Send size={16} />;
    if (lower.includes("missing") || lower.includes("info")) return <AlertCircle size={16} />;
    if (lower.includes("submit")) return <FileText size={16} />;
    if (lower.includes("created")) return <Clock size={16} />;
    return <Clock size={16} />;
  };

  const formatDate = (dateStr) => {
    if (!dateStr) return "";
    try {
      return new Date(dateStr).toLocaleString();
    } catch {
      return dateStr;
    }
  };

  if (!timeline || timeline.length === 0) {
    return (
      <div className="rt-timeline">
        <h3 className="rt-title">{t("timeline")}</h3>
        <div className="rt-empty">{t("no_timeline_events")}</div>
      </div>
    );
  }

  return (
    <div className="rt-timeline">
      <h3 className="rt-title">{t("timeline")}</h3>
      <div className="rt-items">
        {timeline.map((item, idx) => (
          <div key={idx} className="rt-item">
            <div className="rt-icon">{getIcon(item.action)}</div>
            <div className="rt-content">
              <span className="rt-action">{item.action}</span>
              <span className="rt-date">{formatDate(item.performedAt || item.date)}</span>
              {item.comment && <p className="rt-comment">{item.comment}</p>}
              {(item.performedByRole || item.performedBy) && (
                <span className="rt-performer">
                  {t("by")} {item.performedByRole || item.performedBy}
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