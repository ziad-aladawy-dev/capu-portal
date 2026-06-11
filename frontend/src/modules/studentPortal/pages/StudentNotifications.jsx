import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Bell, CheckCheck, Check, Info, AlertTriangle, AlertCircle } from "lucide-react";
import { getLocalized } from "../../../core/utils/getLocalized";
import { NOTIFICATION_TYPE } from "../../../core/services/notificationService";
import PortalPageShell from "../components/shared/PortalPageShell";
import PortalCard from "../components/shared/PortalCard";
import PortalBadge from "../components/shared/PortalBadge";
import PortalSkeleton from "../components/shared/PortalSkeleton";
import PortalEmptyState from "../components/shared/PortalEmptyState";
import { useNotifications, useMarkRead, useMarkAllRead } from "../hooks/useNotifications";
import styles from "./StudentNotifications.module.css";

const PAGE_SIZE = 20;

const TYPE_META = {
  [NOTIFICATION_TYPE.Info]: { Icon: Info, tone: "info" },
  [NOTIFICATION_TYPE.Warning]: { Icon: AlertTriangle, tone: "warning" },
  [NOTIFICATION_TYPE.Error]: { Icon: AlertCircle, tone: "danger" },
};

function timeAgo(iso, t) {
  if (!iso) return "";
  const diff = (Date.now() - new Date(iso).getTime()) / 1000;
  if (diff < 60) return t("portal_notifications.just_now", { defaultValue: "just now" });
  if (diff < 3600) return t("portal_notifications.m_ago", { defaultValue: "{{count}}m ago", count: Math.floor(diff / 60) });
  if (diff < 86400) return t("portal_notifications.h_ago", { defaultValue: "{{count}}h ago", count: Math.floor(diff / 3600) });
  return t("portal_notifications.d_ago", { defaultValue: "{{count}}d ago", count: Math.floor(diff / 86400) });
}

function StudentNotifications() {
  const { t, i18n } = useTranslation();
  const list = useNotifications();
  const markRead = useMarkRead();
  const markAll = useMarkAllRead();

  const [filter, setFilter] = useState("all");
  const [visibleCount, setVisibleCount] = useState(PAGE_SIZE);

  const notifications = useMemo(
    () => [...(list.data || [])].sort((a, b) => new Date(b.createdAt ?? 0) - new Date(a.createdAt ?? 0)),
    [list.data]
  );
  const unreadCount = notifications.filter((n) => !n.isRead).length;

  const filtered = notifications.filter((n) => {
    if (filter === "unread") return !n.isRead;
    if (filter === "read") return n.isRead;
    return true;
  });
  const visible = filtered.slice(0, visibleCount);

  const filters = [
    { key: "all", label: t("portal_notifications.all", { defaultValue: "All" }), count: notifications.length },
    { key: "unread", label: t("portal_notifications.unread", { defaultValue: "Unread" }), count: unreadCount },
    { key: "read", label: t("portal_notifications.read", { defaultValue: "Read" }), count: notifications.length - unreadCount },
  ];

  return (
    <PortalPageShell
      title={t("portal_notifications.title", { defaultValue: "Notifications" })}
      subtitle={t("portal_notifications.subtitle", { defaultValue: "Alerts and updates from the university" })}
      actions={
        unreadCount > 0 ? (
          <button
            type="button"
            className={styles.markAllBtn}
            onClick={() => markAll.mutate()}
            disabled={markAll.isPending}
          >
            <CheckCheck size={14} /> {t("portal_notifications.mark_all", { defaultValue: "Mark all read" })}
          </button>
        ) : undefined
      }
    >
      <div className={styles.filters}>
        {filters.map((f) => (
          <button
            key={f.key}
            type="button"
            className={`${styles.filter} ${filter === f.key ? styles.filterActive : ""}`}
            onClick={() => { setFilter(f.key); setVisibleCount(PAGE_SIZE); }}
          >
            {f.label} <span className={styles.filterCount}>{f.count}</span>
          </button>
        ))}
      </div>

      {list.isLoading ? (
        <div className={styles.list}>
          {Array.from({ length: 5 }).map((_, i) => <PortalSkeleton key={i} variant="block" height={72} />)}
        </div>
      ) : list.isError ? (
        <PortalEmptyState
          icon={AlertCircle}
          title={t("portal_notifications.load_failed", { defaultValue: "Couldn't load notifications" })}
          onAction={() => list.refetch()}
          actionLabel={t("retry", { defaultValue: "Retry" })}
        />
      ) : filtered.length === 0 ? (
        <PortalEmptyState
          icon={Bell}
          title={filter === "unread"
            ? t("portal_notifications.all_caught_up", { defaultValue: "You're all caught up!" })
            : t("portal_notifications.empty", { defaultValue: "No notifications yet" })}
          text={t("portal_notifications.empty_hint", { defaultValue: "When the university has something for you, it appears here." })}
        />
      ) : (
        <>
          <div className={styles.list}>
            {visible.map((n) => {
              const meta = TYPE_META[n.type] || TYPE_META[NOTIFICATION_TYPE.Info];
              return (
                <PortalCard key={n.id} className={`${styles.item} ${n.isRead ? styles.itemRead : ""}`}>
                  <span className={`${styles.itemIcon} ${styles[meta.tone]}`}>
                    <meta.Icon size={16} />
                  </span>
                  <div className={styles.itemBody}>
                    <div className={styles.itemHead}>
                      <strong className={styles.itemTitle}>{getLocalized(n.title, i18n.language)}</strong>
                      {!n.isRead && <span className={styles.unreadDot} aria-label={t("portal_notifications.unread", { defaultValue: "Unread" })} />}
                    </div>
                    {n.body && <p className={styles.itemText}>{getLocalized(n.body, i18n.language)}</p>}
                    <div className={styles.itemMeta}>
                      <PortalBadge tone={meta.tone}>
                        {t(`portal_notifications.type_${meta.tone}`, { defaultValue: meta.tone })}
                      </PortalBadge>
                      <span className={styles.time}>{timeAgo(n.createdAt, t)}</span>
                    </div>
                  </div>
                  {!n.isRead && (
                    <button
                      type="button"
                      className={styles.readBtn}
                      onClick={() => markRead.mutate(n.id)}
                      title={t("portal_notifications.mark_read", { defaultValue: "Mark read" })}
                    >
                      <Check size={14} />
                    </button>
                  )}
                </PortalCard>
              );
            })}
          </div>
          {filtered.length > visibleCount && (
            <button type="button" className={styles.loadMore} onClick={() => setVisibleCount((c) => c + PAGE_SIZE)}>
              {t("portal_notifications.load_more", { defaultValue: "Load more" })} ({filtered.length - visibleCount})
            </button>
          )}
        </>
      )}
    </PortalPageShell>
  );
}

export default StudentNotifications;
