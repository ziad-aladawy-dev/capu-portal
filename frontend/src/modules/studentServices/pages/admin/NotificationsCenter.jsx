import { useState, useEffect, useMemo } from "react";
import { useTranslation } from "react-i18next";
import { Bell, CheckCheck, Info, AlertCircle, ChevronLeft, ChevronRight } from "lucide-react";
import apiClient from "../../../../core/api/apiClient";
import LoadingSpinner from "../../../../core/components/LoadingSpinner";
import EmptyState from "../../../../core/components/EmptyState";
import ErrorMessage from "../../../../core/components/ErrorMessage";
import PageHeader from "../../../../core/components/PageHeader";
import "../../styles/admin/NotificationsCenter.css";

const TYPE_ICONS = {
  1: <Info size={18} />,
  2: <AlertCircle size={18} />,
};

const PAGE_SIZE = 15;

const NotificationsCenter = () => {
  const { t } = useTranslation();
  const [notifications, setNotifications] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [filter, setFilter] = useState("all");
  const [page, setPage] = useState(1);

  const loadNotifications = async () => {
    try {
      const response = await apiClient.get("/notifications");
      setNotifications(Array.isArray(response.data) ? response.data : []);
      setError(null);
    } catch (err) {
      console.error("Failed to load notifications", err);
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  // eslint-disable-next-line react-hooks/set-state-in-effect -- async loader; setState only after await
  useEffect(() => { loadNotifications(); }, []);

  const markAsRead = async (id) => {
    try { await apiClient.put(`/notifications/${id}/read`); await loadNotifications(); } catch (err) { console.error(err); }
  };

  const unread = useMemo(() => notifications.filter((n) => !n.isRead), [notifications]);

  const markAllAsRead = async () => {
    if (unread.length === 0) return;
    try {
      await apiClient.put("/notifications/read", { ids: unread.map((n) => n.id) });
      await loadNotifications();
    } catch (err) { console.error(err); }
  };

  const filtered = useMemo(() => {
    return notifications.filter((n) => {
      if (filter === "unread") return !n.isRead;
      if (filter === "read") return n.isRead;
      return true;
    });
  }, [notifications, filter]);

  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  const paged = useMemo(() => {
    const start = (page - 1) * PAGE_SIZE;
    return filtered.slice(start, start + PAGE_SIZE);
  }, [filtered, page]);

  const getIcon = (type) => TYPE_ICONS[Number(type)] || <Info size={18} />;

  if (loading) return <LoadingSpinner />;
  return (
    <div className="nc-container">
      <PageHeader
        icon={Bell}
        title={t("notifications")}
        subtitle={`${unread.length} ${t("unread")}`}
        actions={
          <button className="btn-secondary" onClick={markAllAsRead} disabled={unread.length === 0}>
            <CheckCheck size={16} /> {t("mark_all_read")}
          </button>
        }
      />
      {error && <ErrorMessage message={error} />}
      <div className="nc-filters">
        <button className={filter === "all" ? "active" : ""} onClick={() => { setFilter("all"); setPage(1); }}>
          {t("all")} ({notifications.length})
        </button>
        <button className={filter === "unread" ? "active" : ""} onClick={() => { setFilter("unread"); setPage(1); }}>
          {t("unread")} ({unread.length})
        </button>
        <button className={filter === "read" ? "active" : ""} onClick={() => { setFilter("read"); setPage(1); }}>
          {t("read")} ({notifications.filter((n) => n.isRead).length})
        </button>
      </div>
      {paged.length === 0 ? (
        <EmptyState icon={Bell} title={t("no_notifications")} />
      ) : (
        <div className="nc-list">
          {paged.map((notif) => (
            <div key={notif.id} className={`nc-item ${!notif.isRead ? "nc-unread" : ""}`}>
              <div className="nc-icon">{getIcon(notif.type)}</div>
              <div className="nc-content">
                <h4>{notif.title}</h4>
                <p>{notif.message}</p>
                <span>{new Date(notif.createdAt).toLocaleString()}</span>
              </div>
              <div className="nc-actions-buttons">
                {!notif.isRead && (
                  <button className="btn-icon" onClick={() => markAsRead(notif.id)} title={t("mark_as_read")}>
                    <CheckCheck size={16} />
                  </button>
                )}
              </div>
            </div>
          ))}
        </div>
      )}
      {totalPages > 1 && (
        <div className="nc-pagination">
          <button className="btn-icon" onClick={() => setPage((p) => Math.max(1, p - 1))} disabled={page === 1}>
            <ChevronLeft size={16} />
          </button>
          <span className="nc-page-info">{t("page")} {page} / {totalPages}</span>
          <button className="btn-icon" onClick={() => setPage((p) => Math.min(totalPages, p + 1))} disabled={page === totalPages}>
            <ChevronRight size={16} />
          </button>
        </div>
      )}
    </div>
  );
};

export default NotificationsCenter;
