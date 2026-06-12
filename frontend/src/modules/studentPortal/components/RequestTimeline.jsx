import { useTranslation } from "react-i18next";
import {
  CheckCircle, Clock, AlertCircle, Send, UserCheck, XCircle, CreditCard, FileText, PlusCircle,
} from "lucide-react";
import { REQUEST_STATUS_NAMES, REQUEST_STATUS } from "../constants/requestStatus";
import styles from "./RequestTimeline.module.css";

// Known backend HistoryEntryDto.Action strings → i18n keys (raw value is the fallback).
const ACTION_KEYS = {
  Created: "portal_requests.action_Created",
  Submitted: "portal_requests.action_Submitted",
  PaymentRequired: "portal_requests.action_PaymentRequired",
  PaymentCompleted: "portal_requests.action_PaymentCompleted",
  Assigned: "portal_requests.action_Assigned",
  Comment: "portal_requests.action_Comment",
};

function getIcon(action) {
  const lower = (action || "").toLowerCase();
  if (lower.includes("approve")) return <CheckCircle size={16} />;
  if (lower.includes("reject")) return <XCircle size={16} />;
  if (lower.includes("assign")) return <UserCheck size={16} />;
  if (lower.includes("comment")) return <Send size={16} />;
  if (lower.includes("payment")) return <CreditCard size={16} />;
  if (lower.includes("submit")) return <FileText size={16} />;
  if (lower.includes("created")) return <PlusCircle size={16} />;
  if (lower.includes("moreinfo") || lower.includes("info")) return <AlertCircle size={16} />;
  return <Clock size={16} />;
}

// Backend HistoryEntryDto: { action, comment, performedByUserId, performedByRole, performedAt }.
function RequestTimeline({ timeline = [] }) {
  const { t } = useTranslation();

  const actionLabel = (action) => {
    if (!action) return "";
    if (ACTION_KEYS[action]) return t(ACTION_KEYS[action], { defaultValue: action });
    // "StatusChanged_Approved" → "Status changed: Approved" (status part localized).
    if (action.startsWith("StatusChanged_")) {
      const statusName = action.slice("StatusChanged_".length);
      const statusId = REQUEST_STATUS[statusName];
      const localizedStatus = statusId
        ? t(`portal_requests.status_${REQUEST_STATUS_NAMES[statusId]}`, { defaultValue: statusName })
        : statusName;
      return `${t("portal_requests.action_StatusChanged", { defaultValue: "Status changed" })}: ${localizedStatus}`;
    }
    // Unknown action — raw fallback, lightly prettified.
    return action.replace(/_/g, " ").replace(/([a-z])([A-Z])/g, "$1 $2").trim();
  };

  const roleLabel = (role) =>
    role ? t(`portal_requests.role_${role}`, { defaultValue: role }) : "";

  const formatDate = (dateStr) => (dateStr ? new Date(dateStr).toLocaleString() : "");

  return (
    <div className={styles.timeline}>
      <h3 className={styles.title}>{t("portal_requests.timeline", { defaultValue: "Timeline" })}</h3>
      {!timeline || timeline.length === 0 ? (
        <div className={styles.empty}>
          {t("portal_requests.no_timeline_events", { defaultValue: "No activity yet." })}
        </div>
      ) : (
        <div className={styles.items}>
          {timeline.map((item, idx) => (
            <div key={idx} className={styles.item}>
              <div className={styles.icon}>{getIcon(item.action)}</div>
              <div className={styles.content}>
                <span className={styles.action}>{actionLabel(item.action)}</span>
                <span className={styles.date}>{formatDate(item.performedAt)}</span>
                {item.comment && <p className={styles.comment}>{item.comment}</p>}
                {item.performedByRole && (
                  <span className={styles.performer}>
                    {t("portal_requests.by", { defaultValue: "by" })} {roleLabel(item.performedByRole)}
                  </span>
                )}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

export default RequestTimeline;
