import { useCallback, useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import {
  Bell, Info, AlertTriangle, AlertCircle, CheckCheck, RefreshCw, X,
} from "lucide-react";
import PermissionGate from "../../../core/auth/PermissionGate";
import * as notificationService from "../../../core/services/notificationService";
import "../styles/notifications.css";

const TYPE_ICON = {
  1: Info,
  2: AlertTriangle,
  3: AlertCircle,
};

function NotificationsPage() {
  const { t } = useTranslation();
  const [tab, setTab] = useState("all"); // 'all' | 'unread'
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [marking, setMarking] = useState(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = tab === "unread"
        ? await notificationService.fetchUnreadNotifications()
        : await notificationService.fetchAllNotifications();
      setItems(Array.isArray(data) ? data : []);
    } catch (err) {
      setError(err.message || "Failed to load notifications");
      setItems([]);
    } finally {
      setLoading(false);
    }
  }, [tab]);

  useEffect(() => {
    load();
  }, [load]);

  const handleMarkRead = async (id) => {
    setMarking(id);
    try {
      await notificationService.markNotificationRead(id);
      setItems((prev) =>
        tab === "unread"
          ? prev.filter((n) => n.id !== id)
          : prev.map((n) => (n.id === id ? { ...n, isRead: true } : n))
      );
    } catch (err) {
      setError(err.message || "Failed to mark as read");
    } finally {
      setMarking(null);
    }
  };

  const handleMarkAllRead = async () => {
    const unread = items.filter((n) => !n.isRead);
    if (unread.length === 0) return;
    try {
      await Promise.all(
        unread.map((n) => notificationService.markNotificationRead(n.id))
      );
      await load();
    } catch (err) {
      setError(err.message || "Failed to mark all as read");
    }
  };

  const formatTime = (iso) => {
    if (!iso) return "—";
    const d = new Date(iso);
    const diff = (Date.now() - d.getTime()) / 1000;
    if (diff < 60) return t("just_now");
    if (diff < 3600) return t("min_ago", { count: Math.floor(diff / 60) });
    if (diff < 86400) return t("hours_ago", { count: Math.floor(diff / 3600) });
    if (diff < 604800) return t("days_ago", { count: Math.floor(diff / 86400) });
    return d.toLocaleDateString();
  };

  const unreadCount = useMemo(
    () => items.filter((n) => !n.isRead).length,
    [items]
  );

  return (
    <div className="notifications-page">
      <div className="notifications-header">
        <div className="notifications-header-left">
          <Bell size={22} />
          <div>
            <h1>{t("notifications")}</h1>
            <p>{t("notifications_desc")}</p>
          </div>
        </div>
        <div style={{ display: "flex", gap: 8 }}>
          <button className="notifications-btn notifications-btn-outline" onClick={load}>
            <><RefreshCw size={13} /> {t("refresh")}</>
          </button>
          {unreadCount > 0 && (
            <PermissionGate resource="notifications.notifications" minLevel={2}>
              <button
                className="notifications-btn notifications-btn-outline"
                onClick={handleMarkAllRead}
              >
                <><CheckCheck size={13} /> {t("mark_all_read")}</>
              </button>
            </PermissionGate>
          )}
        </div>
      </div>

      <div className="notifications-toolbar">
        <button
          className={`notifications-tab ${tab === "all" ? "active" : ""}`}
          onClick={() => setTab("all")}
        >
          {t("all")}
        </button>
        <button
          className={`notifications-tab ${tab === "unread" ? "active" : ""}`}
          onClick={() => setTab("unread")}
        >
          {t("unread")} {unreadCount > 0 && tab !== "unread" && `(${unreadCount})`}
        </button>
      </div>

      {error && (
        <div className="notifications-error-banner">
          <AlertTriangle size={16} />
          <span>{error}</span>
          <button
            onClick={() => setError(null)}
            style={{ marginLeft: "auto", background: "transparent", border: "none", cursor: "pointer", color: "#b91c1c" }}
          >
            <X size={14} />
          </button>
        </div>
      )}

      {loading ? (
        <div className="notifications-loading">
          <div className="notifications-spinner" />
          <p>{t("loading_notifications")}</p>
        </div>
      ) : items.length === 0 ? (
        <div className="notifications-empty">
          <Bell size={40} />
          <h3>{tab === "unread" ? t("no_unread_notifications") : t("no_notifications")}</h3>
          <p>
            {tab === "unread"
              ? t("all_caught_up")
              : t("no_notifications_message")}
          </p>
        </div>
      ) : (
        <div className="notifications-list">
          {items.map((n) => {
            const Icon = TYPE_ICON[n.type] || Info;
            return (
              <div
                key={n.id}
                className={`notification-item ${!n.isRead ? "is-unread" : ""}`}
              >
                <div className={`notification-icon type-${n.type}`}>
                  <Icon size={16} />
                </div>
                <div className="notification-body">
                  <h4 className="notification-title">{n.title}</h4>
                  <p className="notification-message">{n.message}</p>
                  <div className="notification-meta">
                    <span>{notificationService.getNotificationTypeLabel(n.type)}</span>
                    <span>·</span>
                    <span>{formatTime(n.createdAt)}</span>
                    {n.referenceType && (
                      <>
                        <span>·</span>
                        <span style={{ fontFamily: "Space Mono, monospace" }}>
                          {n.referenceType}
                        </span>
                      </>
                    )}
                  </div>
                </div>
                {!n.isRead && (
                  <div className="notification-actions">
                    <button
                      className="notification-mark-read"
                      disabled={marking === n.id}
                      onClick={() => handleMarkRead(n.id)}
                    >
                      {marking === n.id ? "…" : t("mark_read")}
                    </button>
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}

export default NotificationsPage;
