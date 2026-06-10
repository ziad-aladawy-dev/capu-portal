import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Bell, CheckCheck, Info, AlertTriangle, Loader2, Inbox } from "lucide-react";
import {
  useNotificationsInfinite, useUnreadCount, useMarkRead, useMarkAllRead,
} from "../hooks/useNotifications";
import { NOTIFICATION_TYPE } from "../../../core/services/notificationService";
import "../styles/StudentNotifications.css";

const FILTERS = ["all", "unread", "read"];

function typeIcon(type) {
  if (type === NOTIFICATION_TYPE.Warning) return <AlertTriangle size={18} />;
  return <Info size={18} />;
}
function typeClass(type) {
  return type === NOTIFICATION_TYPE.Warning ? "snc-warning" : "snc-info";
}

// Bucket label for date grouping.
function dateBucket(dateStr) {
  const d = new Date(dateStr);
  const now = new Date();
  const startOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  const dayMs = 86_400_000;
  if (d >= startOfToday) return "Today";
  if (d >= new Date(startOfToday - dayMs)) return "Yesterday";
  if (d >= new Date(startOfToday - 7 * dayMs)) return "This Week";
  return "Older";
}

const StudentNotifications = () => {
  const { t } = useTranslation();
  const [filter, setFilter] = useState("all");

  const q = useNotificationsInfinite(filter);
  const unreadQ = useUnreadCount();
  const markRead = useMarkRead();
  const markAllRead = useMarkAllRead();

  const items = useMemo(
    () => (q.data?.pages || []).flatMap((p) => p?.items || []),
    [q.data]
  );

  // Group ordered items by date bucket, preserving order.
  const groups = useMemo(() => {
    const out = [];
    let current = null;
    for (const n of items) {
      const b = dateBucket(n.createdAt);
      if (!current || current.bucket !== b) {
        current = { bucket: b, items: [] };
        out.push(current);
      }
      current.items.push(n);
    }
    return out;
  }, [items]);

  const unread = unreadQ.data || 0;

  return (
    <div className="snc-container">
      <div className="snc-header">
        <div className="snc-title">
          <Bell size={20} />
          <h1>{t("notifications", { defaultValue: "Notifications" })}</h1>
          {unread > 0 && <span className="snc-badge">{unread}</span>}
        </div>
        <button
          className="snc-mark-all"
          onClick={() => markAllRead.mutate()}
          disabled={markAllRead.isPending || unread === 0}
        >
          <CheckCheck size={16} /> {t("mark_all_read", { defaultValue: "Mark all read" })}
        </button>
      </div>

      <div className="snc-filters">
        {FILTERS.map((f) => (
          <button key={f} className={filter === f ? "active" : ""} onClick={() => setFilter(f)}>
            {t(f, { defaultValue: f.charAt(0).toUpperCase() + f.slice(1) })}
          </button>
        ))}
      </div>

      {q.isLoading ? (
        <div className="snc-state"><Loader2 size={20} className="snc-spin" /> {t("loading_notifications", { defaultValue: "Loading…" })}</div>
      ) : items.length === 0 ? (
        <div className="snc-state snc-empty">
          <Inbox size={32} />
          <p>{t("no_notifications_message", { defaultValue: "You're all caught up." })}</p>
        </div>
      ) : (
        <div className="snc-list">
          {groups.map((g) => (
            <div key={g.bucket} className="snc-group">
              <div className="snc-group-label">{t(`group_${g.bucket}`, { defaultValue: g.bucket })}</div>
              {g.items.map((n) => (
                <div key={n.id} className={`snc-item ${!n.isRead ? "snc-unread" : ""}`}>
                  <div className={`snc-icon ${typeClass(n.type)}`}>{typeIcon(n.type)}</div>
                  <div className="snc-content">
                    <h4>{n.title}</h4>
                    <p>{n.message}</p>
                    <span className="snc-time">{new Date(n.createdAt).toLocaleString()}</span>
                  </div>
                  {!n.isRead && (
                    <button className="snc-read-btn" title={t("mark_read", { defaultValue: "Mark read" })}
                      onClick={() => markRead.mutate(n.id)} disabled={markRead.isPending}>
                      <CheckCheck size={16} />
                    </button>
                  )}
                </div>
              ))}
            </div>
          ))}

          {q.hasNextPage && (
            <button className="snc-load-more" onClick={() => q.fetchNextPage()} disabled={q.isFetchingNextPage}>
              {q.isFetchingNextPage
                ? <><Loader2 size={15} className="snc-spin" /> {t("loading", { defaultValue: "Loading…" })}</>
                : t("load_more", { defaultValue: "Load more" })}
            </button>
          )}
        </div>
      )}
    </div>
  );
};

export default StudentNotifications;
