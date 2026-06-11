import { Activity } from "lucide-react";
import { useTranslation } from "react-i18next";
import { usePermission } from "../../../core/auth/usePermission";
import { useRecentAuditLogs } from "../../../core/query/useDashboardStats";

const DOT_BY_ACTION = {
  insert: "#16a34a",
  create: "#16a34a",
  update: "#c9a84c",
  edit: "#c9a84c",
  delete: "#dc2626",
  login: "#2e3591",
};

function dotFor(log) {
  const action = String(log.action || "").toLowerCase();
  for (const [k, color] of Object.entries(DOT_BY_ACTION)) {
    if (action.includes(k)) return color;
  }
  return "#2e3591";
}

function relativeTime(iso, locale) {
  if (!iso) return "";
  const then = new Date(iso.endsWith("Z") || iso.includes("+") ? iso : `${iso}Z`);
  const diffSec = Math.round((then.getTime() - Date.now()) / 1000);
  const rtf = new Intl.RelativeTimeFormat(locale, { numeric: "auto" });
  const abs = Math.abs(diffSec);
  if (abs < 60) return rtf.format(diffSec, "second");
  if (abs < 3600) return rtf.format(Math.round(diffSec / 60), "minute");
  if (abs < 86400) return rtf.format(Math.round(diffSec / 3600), "hour");
  return rtf.format(Math.round(diffSec / 86400), "day");
}

function RecentActivities() {
  const { t, i18n } = useTranslation();
  const { can } = usePermission();
  const canAudit = can("system.audit-logs.view");
  const { data: logs = [], isLoading } = useRecentAuditLogs(6, canAudit);

  if (!canAudit) return null;

  return (
    <div className="dashboard-card anim-activities">
      <div className="card-header">
        <h3>{t("recent_activities")}</h3>
        <Activity size={17} color="#c9a84c" />
      </div>

      {isLoading &&
        [0, 1, 2].map((i) => (
          <div className="activity-item" key={i}>
            <span className="activity-dot dk-skeleton" />
            <div style={{ flex: 1 }}>
              <div className="dk-skeleton-line" style={{ height: 12, width: "70%" }} />
            </div>
          </div>
        ))}

      {!isLoading && logs.length === 0 && (
        <p style={{ fontSize: 12.5, color: "var(--color-text-secondary)", margin: 0 }}>
          {t("dash_recent_activity_empty")}
        </p>
      )}

      {!isLoading &&
        logs.map((log) => (
          <div className="activity-item" key={log.id || `${log.createdAtUtc}-${log.entityName}`}>
            <span className="activity-dot" style={{ background: dotFor(log) }} />
            <div className="activity-body">
              <p>{log.message || `${log.action || ""} ${log.entityName || ""}`.trim() || log.source}</p>
              <span>
                {[log.userName, log.source].filter(Boolean).join(" · ")}
              </span>
            </div>
            <span className="activity-time">{relativeTime(log.createdAtUtc, i18n.language)}</span>
          </div>
        ))}
    </div>
  );
}

export default RecentActivities;
