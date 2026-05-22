import { useState, useEffect, useCallback } from "react";
import {
  Bell, BellOff, Info, AlertTriangle, AlertCircle, CheckCheck, X,
  RefreshCw, Mail, MailOpen, Trash2, Calendar
} from "lucide-react";
import * as notificationService from "../../../core/services/notificationService";
import "../styles/notifications.css";

const TYPE_CONFIG = {
  1: { icon: Info, label: "Info", className: "nt-type-info" },
  2: { icon: AlertTriangle, label: "Warning", className: "nt-type-warning" },
  3: { icon: AlertCircle, label: "Error", className: "nt-type-error" },
};

function NotificationsPage() {
  const [notifications, setNotifications] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [filter, setFilter] = useState("all");

  const fetchData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const all = await notificationService.fetchNotifications();
      setNotifications(all || []);
    } catch (err) {
      setError(err.message || "Failed to load notifications");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { fetchData(); }, [fetchData]);

  const handleMarkRead = async (id) => {
    try {
      await notificationService.markNotificationRead(id);
      setNotifications((prev) =>
        prev.map((n) => (n.id === id ? { ...n, isRead: true } : n))
      );
    } catch {
      // silently fail
    }
  };

  const handleMarkAllRead = async () => {
    const unread = notifications.filter((n) => !n.isRead);
    await Promise.all(unread.map((n) => handleMarkRead(n.id)));
  };

  const formatDateTime = (iso) => {
    if (!iso) return "";
    try {
      const d = new Date(iso);
      const now = new Date();
      const diffMs = now - d;
      const diffMins = Math.floor(diffMs / 60000);
      if (diffMins < 1) return "Just now";
      if (diffMins < 60) return `${diffMins}m ago`;
      const diffHours = Math.floor(diffMins / 60);
      if (diffHours < 24) return `${diffHours}h ago`;
      const diffDays = Math.floor(diffHours / 24);
      if (diffDays < 7) return `${diffDays}d ago`;
      return d.toLocaleDateString("en-EG", { year: "numeric", month: "short", day: "numeric" });
    } catch { return ""; }
  };

  const filtered = filter === "all" ? notifications : notifications.filter((n) => !n.isRead);
  const unreadCount = notifications.filter((n) => !n.isRead).length;

  if (loading && notifications.length === 0) {
    return (
      <div className="nt-page">
        <div className="nt-loading">
          <div className="nt-spinner" />
          <p>Loading notifications…</p>
        </div>
      </div>
    );
  }

  if (error && notifications.length === 0) {
    return (
      <div className="nt-page">
        <div className="nt-header">
          <div className="nt-header-left">
            <Bell size={20} />
            <div><h1>Notifications</h1><p>System notifications and alerts</p></div>
          </div>
        </div>
        <div className="nt-error">
          <BellOff size={36} />
          <h3>Failed to load notifications</h3>
          <p>{error}</p>
          <button className="nt-btn nt-btn-outline" onClick={fetchData}><RefreshCw size={13} /> Retry</button>
        </div>
      </div>
    );
  }

  return (
    <div className="nt-page">
      <div className="nt-header">
        <div className="nt-header-left">
          <div className="nt-header-icon"><Bell size={20} /></div>
          <div>
            <h1>Notifications</h1>
            <p>System notifications and alerts</p>
          </div>
        </div>
        <div className="nt-header-actions">
          {unreadCount > 0 && (
            <button className="nt-btn nt-btn-primary" onClick={handleMarkAllRead}>
              <CheckCheck size={13} /> Mark All Read
            </button>
          )}
          <button className="nt-btn nt-btn-soft" onClick={fetchData}>
            <RefreshCw size={13} /> Refresh
          </button>
        </div>
      </div>

      <div className="nt-toolbar">
        <div className="nt-filter-tabs">
          <button className={`nt-filter-tab ${filter === "all" ? "active" : ""}`} onClick={() => setFilter("all")}>
            All ({notifications.length})
          </button>
          <button className={`nt-filter-tab ${filter === "unread" ? "active" : ""}`} onClick={() => setFilter("unread")}>
            Unread ({unreadCount})
          </button>
        </div>
      </div>

      {error && (
        <div className="nt-banner nt-banner-error">
          <AlertTriangle size={14} /><span>{error}</span>
          <button onClick={() => setError(null)}><X size={12} /></button>
        </div>
      )}

      {filtered.length === 0 ? (
        <div className="nt-empty">
          <BellOff size={40} />
          <h3>{filter === "unread" ? "No unread notifications" : "No notifications found"}</h3>
          <p>{filter === "unread" ? "You're all caught up!" : "Notifications will appear here."}</p>
        </div>
      ) : (
        <div className="nt-list">
          {filtered.map((n) => {
            const TypeIcon = TYPE_CONFIG[n.type]?.icon || Info;
            const typeClass = TYPE_CONFIG[n.type]?.className || "nt-type-info";

            return (
              <div
                key={n.id}
                className={`nt-item ${n.isRead ? "nt-read" : "nt-unread"}`}
                onClick={() => !n.isRead && handleMarkRead(n.id)}
                onKeyDown={(e) => { if ((e.key === "Enter" || e.key === " ") && !n.isRead) { e.preventDefault(); handleMarkRead(n.id); } }}
                role="button"
                tabIndex={0}
                aria-label={n.isRead ? "Read notification" : "Mark as read"}
              >
                <div className={`nt-type-icon ${typeClass}`}>
                  <TypeIcon size={15} />
                </div>
                <div className="nt-content">
                  <div className="nt-title-row">
                    <strong>{n.title}</strong>
                    {!n.isRead && <span className="nt-unread-dot" />}
                  </div>
                  <p className="nt-message">{n.message}</p>
                  <div className="nt-meta">
                    <span className="nt-time"><Calendar size={10} />{formatDateTime(n.createdAt)}</span>
                    {n.referenceType && <span className="nt-ref">{n.referenceType}</span>}
                  </div>
                </div>
                <div className="nt-actions">
                  {n.isRead ? <MailOpen size={13} className="nt-read-icon" /> : <Mail size={13} className="nt-mail-icon" />}
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}

export default NotificationsPage;
